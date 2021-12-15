using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

using Microsoft.VisualBasic.FileIO;

using CS301_batch.Models;

namespace CS301_batch
{
    static class Parameters
    {
        public static string FTP_HOST;
        public static string FTP_PASSWORD;
        public static string FTP_USERNAME;
        public static string S3_BUCKET_TRANSACTIONS;
        public static string SQS_URL;
    }

    class Program
    {
        private static void DownloadCSV(string ftpHost, string ftpUsername, string ftpPassword, string ftpPath, string filePath)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://" + ftpHost + ftpPath);
            request.Method = WebRequestMethods.Ftp.DownloadFile;

            request.Credentials = new NetworkCredential(ftpUsername, ftpPassword);

            FtpWebResponse response = (FtpWebResponse)request.GetResponse();

            Stream responseStream = response.GetResponseStream();
            Stream fileStream = File.Create(filePath);
            responseStream.CopyTo(fileStream);

            Console.WriteLine($"Download Complete, status {response.StatusDescription}");

            fileStream.Close();
            response.Close();
        }

        private static async Task UploadCSV(IAmazonS3 s3client, string filePath, string bucketName, string objectKey)
        {
            TransferUtility fileTransferUtility = new TransferUtility(s3client);
            await fileTransferUtility.UploadAsync(filePath, bucketName, objectKey);
        }

        private static async Task BatchEnqueueTransactions(IAmazonSQS sqsClient, string qUrl, List<Transaction> transactions)
        {
            var messages = new List<SendMessageBatchRequestEntry>();
            int i = 0;

            foreach (Transaction transaction in transactions)
            {
                var message = new SendMessageBatchRequestEntry();
                message.Id = transaction.id;
                message.MessageBody = JsonSerializer.Serialize(transaction);
                message.MessageGroupId = "group";
                messages.Add(message);

                i++;
            }
            SendMessageBatchResponse responseSendBatch = await sqsClient.SendMessageBatchAsync(qUrl, messages);
        }

        private static void ParseCSV(IAmazonSQS sqsClient, string qUrl, string filePath)
        {
            using (TextFieldParser csvParser = new TextFieldParser(filePath))
            {
                csvParser.CommentTokens = new string[] { "#" };
                csvParser.SetDelimiters(new string[] { "," });
                csvParser.HasFieldsEnclosedInQuotes = true;

                // Skip the row with the column names
                csvParser.ReadLine();

                while (!csvParser.EndOfData)
                {
                    var transactions = new List<Transaction>();
                    for (int i = 0; i < 10; i++)
                    {
                        if (csvParser.EndOfData)
                        {
                            break;
                        }

                        string[] fields = csvParser.ReadFields();
                        Transaction transaction = new Transaction();

                        transaction.id = fields[0];
                        transaction.transaction_id = fields[1];
                        transaction.merchant = fields[2];
                        transaction.mcc = fields[3];
                        transaction.currency = fields[4];
                        transaction.amount = fields[5];
                        transaction.transaction_date = fields[6];
                        transaction.card_id = fields[7];
                        transaction.card_pan = fields[8];
                        transaction.card_type = fields[9];

                        transactions.Add(transaction);
                    }

                    BatchEnqueueTransactions(sqsClient, qUrl, transactions).Wait();
                }
            }
        }

        private static async Task GetParams(RegionEndpoint region)
        {
            using (var client = new AmazonSimpleSystemsManagementClient(region))
            {
                try
                {
                    var paramNames = new List<string>
                    {
                            "FTP_HOST",
                            "FTP_PASSWORD",
                            "FTP_USERNAME",
                            "S3_BUCKET_TRANSACTIONS",
                            "SQS_URL"
                    };

                    var req = new GetParametersRequest()
                    {
                        Names = paramNames,
                        WithDecryption = true
                    };
                    
                    var res = await client.GetParametersAsync(req);
                    
                    Parameters.FTP_HOST = res.Parameters[0].Value;
                    Parameters.FTP_PASSWORD = res.Parameters[1].Value;
                    Parameters.FTP_USERNAME = res.Parameters[2].Value;
                    Parameters.S3_BUCKET_TRANSACTIONS = res.Parameters[3].Value;
                    Parameters.SQS_URL = res.Parameters[4].Value;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error occurred: {ex.Message}");
                }
            }
        }

        static void Main(string[] args)
        {
            var region = Amazon.RegionEndpoint.APSoutheast1;
            GetParams(region).Wait();

            // parse manual date from args if provided, else use current date
            var date = args.Length > 0 ? args[0] : DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
            string ftpPath = "/spend/" + date + "/spend.csv";
            string filePath = System.IO.Path.GetFullPath("spend.csv");
            Console.WriteLine($"Downloading file from {Parameters.FTP_HOST}{ftpPath}");

            try
            {
                DownloadCSV(Parameters.FTP_HOST, Parameters.FTP_USERNAME, Parameters.FTP_PASSWORD, ftpPath, filePath);
            }
            catch (System.Net.WebException ex)
            {
                var res = (FtpWebResponse) ex.Response;
                Console.Error.WriteLine($"FTP server returned with error {res.StatusCode}: {res.StatusDescription}");
                return;
            }

            IAmazonS3 s3client = new AmazonS3Client(region);
            UploadCSV(s3client, filePath, Parameters.S3_BUCKET_TRANSACTIONS, ftpPath).Wait();

            Console.WriteLine($"Enqueueing to {Parameters.SQS_URL}");
            IAmazonSQS sqsClient = new AmazonSQSClient();
            ParseCSV(sqsClient, Parameters.SQS_URL, filePath);
            
        }
    }
}
