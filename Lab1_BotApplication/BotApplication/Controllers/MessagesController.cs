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
                Initialize();
            }

            if (activity.Type == ActivityTypes.Message)
            {
                ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                try
                {
                    var tags = await AnalyzeTags(activity.Text);
                    var words = await AnalyzeWords(activity.Text);
                    
                    for (int i = 0; i < words.Count; i++)
                    {
                        var currentTypes = _testBotAIEntities.WordTypes.ToList();


                        if (currentTypes.All(x => x.Name != tags[i]))
                        {
                            var newWordType = new WordType() { Name = tags[i] };
                            _testBotAIEntities.WordTypes.Add(newWordType);
                            _testBotAIEntities.SaveChanges();
                        }

                        currentTypes = _testBotAIEntities.WordTypes.ToList();
                        var currentWords = _testBotAIEntities.Words.ToList();
                        if (currentWords.All(x => x.Name != words[i]))
                        {
                            var newWord = new Word() { Name = words[i], WordType = currentTypes.First(x => x.Name == tags[i]) };
                            _testBotAIEntities.Words.Add(newWord);
                            _testBotAIEntities.SaveChanges();
                        }


                    }

                    var alllWords = _testBotAIEntities.Words.ToList();
                    var sb = new StringBuilder();

                    var rand = new Random();
                    for (int i = 0; i < tags.Count; i++)
                    {
                        var possibleWords = alllWords.Where(x => x.WordType.Name == tags[i]).ToList();
                        sb.Append(possibleWords.ElementAt(rand.Next(possibleWords.Count)).Name + " ");
                    }


                    Activity reply = activity.CreateReply(sb.ToString());
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
        

        public static async void Initialize()
        {
            if (_testBotAIEntities == null)
            {
                _testBotAIEntities = new TestBotAIEntities();
            }

            if (_linguisticsClient == null)
            {
                _linguisticsClient = new LinguisticsClient(WebConfigurationManager.AppSettings["LinguisticsClientKey"]);

            }

            if (_analyzerTagGuid == Guid.Empty || _analyzerWordGuid == Guid.Empty)
            {
                var Analyzers = await _linguisticsClient.ListAnalyzersAsync();

                _analyzerTagGuid = Analyzers[0].Id;
                _analyzerWordGuid = Analyzers[2].Id;
            }
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