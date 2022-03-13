using Newtonsoft.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace ICMatrixAssessment
{
    class Program
    {
        static string baseUrl = "https://recruitment-test.investcloud.com/";
        static int datasetInitSize = 1000;
        static HttpClient client = new HttpClient();

        static void Main(string[] args) => RunProgram().GetAwaiter().GetResult();

        static async Task RunProgram()
        {

            client.BaseAddress = new Uri(baseUrl);
            Stopwatch timer = new Stopwatch();

            Responses response = await InitDataset(datasetInitSize);
            timer.Start();

            int[][]? datasetA = GenerateMatrix(datasetInitSize);
            int[][]? datasetB = GenerateMatrix(datasetInitSize);

            Console.WriteLine("Local Matrix Generation...success!");
            Console.WriteLine("Start synchronous dataset fetch...");

            Parallel.Invoke(() =>
            {
                Task<int[][]> fetchA = CreateABMatrix(datasetInitSize, "A");
                datasetA = fetchA.Result;
                Console.WriteLine("Dataset A fetch complete");
            },

            () =>
            {
                Task<int[][]> fetchB = CreateABMatrix(datasetInitSize, "B");
                datasetB = fetchB.Result;
                Console.WriteLine("Dataset B fetch complete");
            });

            Console.WriteLine("Remote Dataset A/B fetch...success!");
            Console.WriteLine("Multiplying Matrices...");

            int[][]? datasetC = MultiplyMatrices(datasetA, datasetB);
            List<int[]> results = datasetC.ToList();
            StringBuilder accString = new StringBuilder();
            foreach (int[] array in results)
            {
                accString.Append(String.Join("", array));
            }
            string hashedString = GetHash(accString.ToString());
            Console.WriteLine("Verifying Hashed string: {0}", hashedString);
            await VerifyHash(hashedString);
            Console.WriteLine("Verification...{0}", response.Success ? "Success!" : "Fail");
            timer.Stop();
            TimeSpan timespan = timer.Elapsed;
            string computationTime = String.Format("{0:00} minutes and {1:00}.{2:00} seconds", timespan.Minutes, timespan.Seconds, timespan.Milliseconds / 10);
            Console.WriteLine("Completed in {0}", computationTime);
            Console.ReadLine();
        }

        static async Task<Responses> InitDataset(int datasetSize)
        {
            string initPath = string.Format(baseUrl + "api/numbers/init/" + datasetSize);
            string content = string.Empty;
            HttpResponseMessage response = await client.GetAsync(initPath);

            content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Remote Dataset initialization...success!");
            }

            Responses? result = JsonConvert.DeserializeObject<Responses>(content);
            return result!;
        }

        static Task<int[][]> CreateABMatrix(int datasetSize, string dataset)
        {
            int[][] array = GenerateMatrix(datasetSize);
            var list = new List<int>();

            var listResults = new List<string>();
            for (int i = 0; i < datasetSize; i++)
            {
                list.Add(i);
            }

            Parallel.ForEach(list, new ParallelOptions() { MaxDegreeOfParallelism = 1 }, index =>
            {
                HttpResponseMessage response = client.GetAsync(baseUrl + "api/numbers/" + dataset + "/row/" + index.ToString()).Result;

                var contents = response.Content.ReadAsStringAsync().Result;
                listResults.Add(contents);
                MatrixResponse? result = JsonConvert.DeserializeObject<MatrixResponse>(contents);
                if (result?.Value != null)
                {
                    array[index] = result.Value;
                }
                Console.Write("\r{0}: #{1}      ", dataset, index);
            });
            return Task.FromResult(array);
        }

        static int[][] GenerateMatrix(int datasetSize)
        {
            int[][] newMatrix = new int[datasetSize][];
            for (int i = 0; i < datasetSize; i++)
                newMatrix[i] = new int[datasetSize];
            return newMatrix;
        }
        static int[][] MultiplyMatrices(int[][] datasetA, int[][] datasetB)
        {
            int size = datasetA.Length;
            int[][] product = GenerateMatrix(size);
            ParallelLoopResult parallelLoopResult = Parallel.For(0, size, i =>
            {
                for (int j = 0; j < size; j++)
                {
                    for (int k = 0; k < size; k++)
                    {
                        product[i][j] += datasetA[i][k] * datasetB[k][j];
                    }
                }
            });
            return product;
        }

        static string GetHash(string input)
        {
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputInBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hashedString = md5.ComputeHash(inputInBytes);

            StringBuilder accString = new StringBuilder();
            for (int i = 0; i < hashedString.Length; i++)
            {
                accString.Append(hashedString[i].ToString("X2"));
            }
            return accString.ToString();
        }

        static async Task VerifyHash(string input)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://recruitment-test.investcloud.com/");
                StringContent content = new StringContent(input, Encoding.UTF8, "application/json");
                var result = await client.PostAsync("api/numbers/validate", content);
                string resultContent = await result.Content.ReadAsStringAsync();
            }
        }
    }
}