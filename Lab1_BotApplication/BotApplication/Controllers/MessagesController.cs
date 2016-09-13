using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Microsoft.ProjectOxford.Linguistics;
using Microsoft.ProjectOxford.Linguistics.Contract;
using Newtonsoft.Json.Linq;

namespace Bot_Application1
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        private static LinguisticsClient _linguisticsClient;

        private static Guid _analyzerTagGuid;

        private static Guid _analyzerWordGuid;

        private static TestBotAIEntities _testBotAIEntities;

        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody] Activity activity)
        {
            if (_linguisticsClient == null || _analyzerTagGuid == Guid.Empty || _analyzerWordGuid == Guid.Empty)
            {
                await Initialize();
            }

            if (activity.Type == ActivityTypes.Message)
            {
                ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                try
                {
                    var responseMessage = await ProceedMessage(activity.Text);
                    
                    Activity reply = activity.CreateReply(responseMessage);
                    await connector.Conversations.ReplyToActivityAsync(reply);
                }
                catch (Exception)
                {
                    Activity reply = activity.CreateReply("Something goes wrong, try another query..");
                    await connector.Conversations.ReplyToActivityAsync(reply);
                }
            }
            else
            {
                HandleSystemMessage(activity);
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private static async Task AddTagsToDictionary(List<KeyValuePair<string, string>> tgwPairList)
        {
            var currentTypes = _testBotAIEntities.WordTypes.ToList();

            foreach (var tag in tgwPairList.Select(x => x.Value).Distinct())
            {
                if (currentTypes.All(x => x.Name != tag))
                {
                    var newWordType = new WordType() { Name = tag };
                    _testBotAIEntities.WordTypes.Add(newWordType);
                }
            }

            _testBotAIEntities.SaveChanges();
        }

        private static async Task AddWordsToDictionary(List<KeyValuePair<string, string>> tgwPairList)
        {
            var currentTypes = _testBotAIEntities.WordTypes.ToList();
            var currentWords = _testBotAIEntities.Words.ToList();

            foreach (var tgw in tgwPairList.Distinct())
            {
                if (currentWords.All(x => x.Name != tgw.Key))
                {
                    var newWord = new Word() { Name = tgw.Key, WordType = currentTypes.First(x => x.Name == tgw.Value) };
                    _testBotAIEntities.Words.Add(newWord);
                }
            }

            _testBotAIEntities.SaveChanges();
        }


        private static async Task FillDictionary(List<KeyValuePair<string, string>> tgwPairList)
        {
            await AddTagsToDictionary(tgwPairList);
            await AddWordsToDictionary(tgwPairList);
        }

        private static async Task<string> ProceedMessage(string msg)
        {
            var tags = await AnalyzeTags(msg);
            var words = await AnalyzeWords(msg);

            var tagsAndWords = words.Zip(tags, (word, tag) => new KeyValuePair<string, string>(word, tag)).ToList();

            // All words, except that come from message
            var filteredWords = _testBotAIEntities.Words.ToList().Where(x => !tagsAndWords.Select(y => y.Key).Contains(x.Name));

            var sb = new StringBuilder();
            var rand = new Random();

            foreach (var tgw in tagsAndWords)
            {
                var possibleWords = filteredWords.Where(x => x.WordType.Name == tgw.Value).ToList();

                if (possibleWords.Count > 0)
                {
                    sb.Append(possibleWords.ElementAt(rand.Next(possibleWords.Count)).Name + " ");
                }
                else
                {
                    sb.Append(tgw.Key);
                }
            }

            FillDictionary(tagsAndWords);

            return sb.ToString();
        }

        public static async Task Initialize()
        {
            if (_testBotAIEntities == null)
            {
                _testBotAIEntities = new TestBotAIEntities();
            }

            if (_linguisticsClient == null)
            {
                _linguisticsClient = new LinguisticsClient(WebConfigurationManager.AppSettings["LinguisticsClientKey"]);

            }

            if (_analyzerTagGuid != Guid.Empty && _analyzerWordGuid != Guid.Empty) return;

            var analyzers = await _linguisticsClient.ListAnalyzersAsync();
            _analyzerTagGuid = analyzers[0].Id;
            _analyzerWordGuid = analyzers[2].Id;
        }

        private static async Task<List<string>> AnalyzeTags(string s)
        {
            var Req = new AnalyzeTextRequest();
            Req.Language = "en";
            Req.Text = s;
            Req.AnalyzerIds = new Guid[] { _analyzerTagGuid };
            var Res = await _linguisticsClient.AnalyzeTextAsync(Req);
            return (Res[0].Result as JArray).First.Select(x => x.ToString()).ToList();
        }

        private static async Task<List<string>> AnalyzeWords(string s)
        {
            var Req = new AnalyzeTextRequest();
            Req.Language = "en";
            Req.Text = s;
            Req.AnalyzerIds = new Guid[] { _analyzerWordGuid };
            var Res = await _linguisticsClient.AnalyzeTextAsync(Req);
            var childrens = (Res[0].Result as JArray).First.Last.First.Children();
            return childrens.Select(x => x.Last.First.Value<string>()).ToList();
        }


        public static void ShowAdj(string s)
        {
            Regex ItemRegex = new Regex(@"\(JJ (\w+)\) \(NN (\w+)\)", RegexOptions.Compiled);
            foreach (Match ItemMatch in ItemRegex.Matches(s))
            {
                Console.WriteLine($"{ItemMatch.Groups[1].ToString()} {ItemMatch.Groups[2].ToString()}");
            }
        }

        private Activity HandleSystemMessage(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                // Handle conversation state changes, like members being added and removed
                // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                // Not available in all channels
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
            }

            return null;
        }
    }
}