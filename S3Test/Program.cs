using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

namespace S3Test
{
    class MainClass
    {
        static readonly string awsAccessKeyId = "REDACTED";
        static readonly string awsSecretAccessKey = "REDACTED";
        static readonly string mfaSerialNumber = "REDACTED";
        static readonly string roleArn = "REDACTED";

        static readonly string bucketName = "REDACTED";
        static readonly string key = "REDACTED";

        public static void Main(string[] args)
        {
            Console.Write("Enter MFA code: ");
            var mfaTOTP = Console.ReadLine();

            var stsClient = new AmazonSecurityTokenServiceClient(awsAccessKeyId, awsSecretAccessKey);

            var credentials = stsClient.AssumeRole(new AssumeRoleRequest
            {
                RoleArn = roleArn,
                RoleSessionName = "dotnettest",
                DurationSeconds = 3600,
                SerialNumber = mfaSerialNumber,
                TokenCode = mfaTOTP
            }).Credentials;

            var regionEndpoint = RegionEndpoint.EUWest1;
            var amazonS3 = new AmazonS3Client(credentials, regionEndpoint);

            Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine("Method A");

                    var bytes = await RetrieveBodyA(amazonS3);

                    Console.WriteLine(Encoding.Default.GetString(bytes).Substring(0, 50));
                }
                catch (NotSupportedException e)
                {
                    Console.WriteLine($"'Method A' Exception: {e.Message}");
                }

                try
                {
                    Console.WriteLine("Method B");

                    var bytes = await RetrieveBodyB(amazonS3);

                    Console.WriteLine(Encoding.Default.GetString(bytes).Substring(0, 50));
                }
                catch (NotSupportedException e)
                {
                    Console.WriteLine($"'Method B' Exception: {e.Message}");
                }
            }).Wait();
        }

        static async Task<byte[]> RetrieveBodyA(IAmazonS3 amazonS3)
        {
            byte[] body;
            var s3GetResponse = await amazonS3.GetObjectAsync(bucketName, key).ConfigureAwait(false);

            Console.WriteLine($"{typeof(Amazon.S3.Model.StreamResponse)} Supports Seek: {s3GetResponse.ResponseStream.CanSeek}");

            body = new byte[s3GetResponse.ResponseStream.Length];

            using (var bufferedStream = new BufferedStream(s3GetResponse.ResponseStream))
            {
                int count;
                var transferred = 0;
                const int maxChunkSize = 8 * 1024;
                var bytesToRead = Math.Min(maxChunkSize, body.Length - transferred);
                while ((count = await bufferedStream.ReadAsync(body, transferred, bytesToRead).ConfigureAwait(false)) > 0)
                {
                    transferred += count;
                    bytesToRead = Math.Min(maxChunkSize, body.Length - transferred);
                }
            }

            return body;
        }

        static async Task<byte[]> RetrieveBodyB(IAmazonS3 amazonS3)
        {
            var s3GetResponse = await amazonS3.GetObjectAsync(bucketName, key).ConfigureAwait(false);

            using (var memoryStream = new MemoryStream())
            {
                Console.WriteLine($"{typeof(MemoryStream)} Supports Seek: {memoryStream.CanSeek}");

                s3GetResponse.ResponseStream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }
}
