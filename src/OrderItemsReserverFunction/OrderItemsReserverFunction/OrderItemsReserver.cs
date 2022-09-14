using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Storage.Files.Shares;
using System.Text;

namespace OrderItemsReserverFunction
{
    public static class OrderItemsReserver
    {
        [FunctionName("OrderItemsReserver")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            var res = false;

            try
            {
                string name = req.Query["name"];

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                byte[] byteArray = Encoding.UTF8.GetBytes(requestBody);
                MemoryStream stream = new MemoryStream(byteArray);
                stream.Position = 0;

                var shareServiceClient = new ShareServiceClient("DefaultEndpointsProtocol=https;AccountName=storageapenkov;AccountKey=wJNnIkbX2mQ0vRHJH2EJpw1ryKbs8bmOA6WUKv0fuDx7cHKweysEQ3PFE6TxRn2l5MH3Jkdu/U8f+AStNy8toQ==;EndpointSuffix=core.windows.net");
                var shareClient = shareServiceClient.GetShareClient("order");
                var shareDirectoryClient = shareClient.GetRootDirectoryClient();

                var shareFileClient = await shareDirectoryClient.CreateFileAsync($"order-{data.ItemId}.json", stream.Length);
                
                var shareFileUploadInfo = shareFileClient.Value.Upload(stream);
                res = !shareFileUploadInfo.GetRawResponse().IsError;
            }
            catch(Exception ex)
            {
                log.LogError(ex, "An exception has occurred");
            }

            return new OkObjectResult(res ? "Success" : "Failed");
        }
    }
}
