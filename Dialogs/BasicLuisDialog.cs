using System;
using System.Configuration;
using System.Threading.Tasks;

using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;

using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text;
using System.Security;
using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.Search.Query;
using Microsoft.Bot.Connector;


namespace Microsoft.Bot.Sample.LuisBot
{
    // For more information about this template visit http://aka.ms/azurebots-csharp-luis
    [Serializable]
    public class BasicLuisDialog : LuisDialog<object>
    {
		static string qnamaker_endpointKey = "830e6887-5031-4227-9818-ff4891b44023";
		static string qnamaker_endpointDomain = "botsharepointsearch";
		static string HR_kbID = "b664476d-faa0-4709-8dc7-f5e3662bd31c";

        private const string EntityKeyword = "SearchKeyword";


        private static readonly Dictionary<string, string> PropertyMappings
        = new Dictionary<string, string>
        {
            { "SearchKeyword", "kbkeyword" }
        };

        [Serializable]
        public class PartialMessage
        {
            public string Text { set; get; }
        }



        private PartialMessage message;

        public QnAMakerService hrQnAService = new QnAMakerService("https://" + qnamaker_endpointDomain + ".azurewebsites.net", HR_kbID, qnamaker_endpointKey);

		
        public BasicLuisDialog() : base(new LuisService(new LuisModelAttribute(
            ConfigurationManager.AppSettings["LuisAppId"], 
            ConfigurationManager.AppSettings["LuisAPIKey"], 
            domain: ConfigurationManager.AppSettings["LuisAPIHostName"])))
        {
        }

        [LuisIntent("None")]
        public async Task NoneIntent(IDialogContext context, LuisResult result)
        {
            await this.ShowLuisResult(context, result);
        }

        // Go to https://luis.ai and create a new intent, then train/publish your luis app.
        // Finally replace "Greeting" with the name of your newly created intent in the following handler
        [LuisIntent("Greeting")]
        public async Task GreetingIntent(IDialogContext context, LuisResult result)
        {
			var qnaMakerAnswer = await hrQnAService.GetAnswer(result.Query);
			await context.PostAsync($"{qnaMakerAnswer}");
			context.Wait(MessageReceived);
            //await this.ShowLuisResult(context, result);
        }

		[LuisIntent("FindDocumentation")]
        public async Task FindIntent(IDialogContext context, LuisResult result)
        {
            var targetSite = new Uri("https://abcatul.sharepoint.com");
            var login = "atul.gupta@ABCAtul.onmicrosoft.com";
            var password = "Nilima1@05";
            StringBuilder sbQuery = new StringBuilder();
            bool QueryTransformed = false;
            var searchKeyword = "";
            if (result.Entities.Count > 0)
            {
                QueryTransformed = true;
                EntityRecommendation keywordEntityRecommendation;

                if (result.TryFindEntity(EntityKeyword, out keywordEntityRecommendation))
                {
                    searchKeyword = keywordEntityRecommendation.Entity;
                    sbQuery.Append(searchKeyword);
                }
               
            }
            else
            {
                //should replace all special chars
                sbQuery.Append(this.message.Text.Replace("?", ""));
            }

            var reply = context.MakeMessage();
            reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            reply.Attachments = new List<Microsoft.Bot.Connector.Attachment>();

            var securePassword = new SecureString();
            foreach (char c in password)
            {
                securePassword.AppendChar(c);
            }
            var onlineCredentials = new SharePointOnlineCredentials(login, securePassword);

            using (ClientContext clientContext = new ClientContext(targetSite))
            {
                clientContext.Credentials = onlineCredentials;

                KeywordQuery query = new KeywordQuery(clientContext);
                query.QueryText = string.Concat(sbQuery.ToString(), " IsDocument:1");
                //query.QueryText = "test";
                query.RowLimit = 5;

                SearchExecutor searchExecutor = new SearchExecutor(clientContext);
                ClientResult<ResultTableCollection> resultShare = searchExecutor.ExecuteQuery(query);
                clientContext.ExecuteQuery();
                if (resultShare.Value != null && resultShare.Value.Count > 0 && resultShare.Value[0].RowCount > 0)
                {
                    reply.Text += (QueryTransformed == true) ? "I found some interesting reading for you!" : "I found some potential interesting reading for you!";
                    BuildReply(resultShare, reply);
                }
                else
                {
                    if (QueryTransformed)
                    {
                        //fallback with the original message
                        query.QueryText = string.Concat(this.message.Text.Replace("?", ""), " IsDocument:1");
                        query.RowLimit = 3;
                        searchExecutor = new SearchExecutor(clientContext);
                        resultShare = searchExecutor.ExecuteQuery(query);
                        clientContext.ExecuteQuery();
                        if (resultShare.Value != null && resultShare.Value.Count > 0 && resultShare.Value[0].RowCount > 0)
                        {
                            reply.Text += "I found some potential interesting reading for you!";
                            BuildReply(resultShare, reply);
                        }
                        else
                            reply.Text += "I could not find any interesting document!";
                    }
                    else
                        reply.Text += "I could not find any interesting document!";

                }
                await context.PostAsync(reply);
                context.Wait(MessageReceived);
            }
        }

        void BuildReply(ClientResult<ResultTableCollection> results, IMessageActivity reply)
        {
            foreach (var row in results.Value[0].ResultRows)
            {
                List<CardAction> cardButtons = new List<CardAction>();
                List<CardImage> cardImages = new List<CardImage>();
                string ct = string.Empty;
                string icon = string.Empty;
                switch (row["FileExtension"].ToString())
                {
                    case "docx":
                        ct = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        icon = "https://cdn2.iconfinder.com/data/icons/metro-ui-icon-set/128/Word_15.png";
                        break;
                    case "xlsx":
                        ct = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        icon = "https://cdn2.iconfinder.com/data/icons/metro-ui-icon-set/128/Excel_15.png";
                        break;
                    case "pptx":
                        ct = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                        icon = "https://cdn2.iconfinder.com/data/icons/metro-ui-icon-set/128/PowerPoint_15.png";
                        break;
                    case "pdf":
                        ct = "application/pdf";
                        icon = "https://cdn4.iconfinder.com/data/icons/CS5/256/ACP_PDF%202_file_document.png";
                        break;

                }
                cardButtons.Add(new CardAction
                {
                    Title = "Open",
                    Value = (row["ServerRedirectedURL"] != null) ? row["ServerRedirectedURL"].ToString() : row["Path"].ToString(),
                    Type = ActionTypes.OpenUrl
                });
                cardImages.Add(new CardImage(url: icon));
                ThumbnailCard tc = new ThumbnailCard();
                tc.Title = (row["Title"] != null) ? row["Title"].ToString() : "Untitled";
                tc.Text = (row["Description"] != null) ? row["Description"].ToString() : string.Empty;
                tc.Images = cardImages;
                tc.Buttons = cardButtons;
                reply.Attachments.Add(tc.ToAttachment());
            }
        }

        private async Task ShowLuisResult(IDialogContext context, LuisResult result) 
        {
            await context.PostAsync($"You have reached {result.Intents[0].Intent}. You said: {result.Query}");
            context.Wait(MessageReceived);
        }
    }
	
	public class Metadata
	{
		public string name { get; set; }
		public string value { get; set; }
	}

	public class Answer
	{
		public IList<string> questions { get; set; }
		public string answer { get; set; }
		public double score { get; set; }
		public int id { get; set; }
		public string source { get; set; }
		public IList<object> keywords { get; set; }
		public IList<Metadata> metadata { get; set; }
	}

	public class QnAAnswer
	{
		public IList<Answer> answers { get; set; }
	}
	
	[Serializable]
	public class QnAMakerService
	{
		private string qnaServiceHostName;
		private string knowledgeBaseId;
		private string endpointKey;

		public QnAMakerService(string hostName, string kbId, string endpointkey)
		{
			qnaServiceHostName = hostName;
			knowledgeBaseId = kbId;
			endpointKey = endpointkey;

		}
		async Task<string> Post(string uri, string body)
		{
			using (var client = new HttpClient())
			using (var request = new HttpRequestMessage())
			{
				request.Method = HttpMethod.Post;
				request.RequestUri = new Uri(uri);
				request.Content = new StringContent(body, Encoding.UTF8, "application/json");
				request.Headers.Add("Authorization", "EndpointKey " + endpointKey);

				var response = await client.SendAsync(request);
				return  await response.Content.ReadAsStringAsync();
			}
		}
		public async Task<string> GetAnswer(string question)
		{
			string uri = qnaServiceHostName + "/qnamaker/knowledgebases/" + knowledgeBaseId + "/generateAnswer";
			string questionJSON = @"{'question': '" + question + "'}";

			var response = await Post(uri, questionJSON);

			var answers = JsonConvert.DeserializeObject<QnAAnswer>(response);
			if (answers.answers.Count > 0)
			{
				return answers.answers[0].answer;
			}
			else
			{
				return "No good match found.";
			}
		}

        
    }	
}