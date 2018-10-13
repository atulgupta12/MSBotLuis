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

namespace Microsoft.Bot.Sample.LuisBot
{
    // For more information about this template visit http://aka.ms/azurebots-csharp-luis
    [Serializable]
    public class BasicLuisDialog : LuisDialog<object>
    {
		static string qnamaker_endpointKey = "830e6887-5031-4227-9818-ff4891b44023";
		static string qnamaker_endpointDomain = "botsharepointsearch";
		static string HR_kbID = "b664476d-faa0-4709-8dc7-f5e3662bd31c";
		
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

        [LuisIntent("Cancel")]
        public async Task CancelIntent(IDialogContext context, LuisResult result)
        {
            await this.ShowLuisResult(context, result);
        }

        [LuisIntent("Help")]
        public async Task HelpIntent(IDialogContext context, LuisResult result)
        {
            await this.ShowLuisResult(context, result);
        }
		
		[LuisIntent("FindDocumentation")]
        public async Task FindIntent(IDialogContext context, LuisResult result)
        {
            await this.ShowLuisResult(context, result);
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