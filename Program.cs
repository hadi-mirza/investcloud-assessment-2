using Newtonsoft.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System;

namespace ICMatrixAssessment
{
    public class Responses
    {
        public string? Value { get; set; }
        public string? Cause { get; set; }
        public bool Success { get; set; }
    }

    public class MatrixResponse
    {
        public int[]? Value { get; set; }
        public string? Cause { get; set; }
        public bool Success { get; set; }
    }

    class Program
    {
        static string baseUrl = "https://recruitment-test.investcloud.com/";
        static int datasetInitSize = 1000;
        static HttpClient client = new HttpClient();

        static void Main(string[] args)
        {
            RunProgram().GetAwaiter().GetResult();
        }

        static async Task RunProgram()
        {

            client.BaseAddress = new Uri(baseUrl);
            Stopwatch timer = new Stopwatch();

            Responses response = await InitDataset(datasetInitSize);

            int[][]? datasetA = null;
            int[][]? datasetB = null;
            int[][]? datasetC = null;


            timer.Start();

            datasetA = GenerateMatrix(datasetInitSize);
            datasetB = GenerateMatrix(datasetInitSize);

            Console.WriteLine("Local Matrix Generation...success!");

            Task<int[][]> fetchA = CreateABMatrix(datasetInitSize, "A");
            Task<int[][]> fetchB = CreateABMatrix(datasetInitSize, "B");

            Console.WriteLine("Fetching dataset from server...");

            Task.WaitAll(fetchA, fetchB);
            datasetA = fetchA.Result;
            datasetB = fetchB.Result;

            Console.WriteLine("Remote Dataset A/B fetch...success!");
            Console.WriteLine("Multplying Matrices...");

            datasetC = MultiplyMatrices(datasetA, datasetB);
            List<int[]> results = datasetC.ToList();
            StringBuilder accString = new StringBuilder();
            foreach (int[] array in results)
            {
                accString.Append(String.Join("", array));
            }
            string hashedString = GetHash(accString.ToString());
            Console.WriteLine("Verifying Hashed string: {0}", hashedString);
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

        static async Task<int[][]> CreateABMatrix(int datasetSize, string dataset)

        {
            int[][] array = GenerateMatrix(datasetSize);

            for (int i = 0; i < datasetSize; i++)
            {
                string content = string.Empty;
                string contentPath = string.Format(baseUrl + "api/numbers/" + dataset + "row" + i.ToString());
                HttpResponseMessage response = await client.GetAsync(contentPath);
                if (response.IsSuccessStatusCode)
                {
                    content = await response.Content.ReadAsStringAsync();
                }
                MatrixResponse? result = JsonConvert.DeserializeObject<MatrixResponse>(content);
                if (result?.Value != null)
                {
                    array[i] = result.Value;

                }
                Console.Write("\r#{0}   ", i);
            }
            return array;
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
            Parallel.For(0, size, i =>
            {
                for (int j = 0; j < size; j++)
                {
                    for (int k = 0; k < size; k++)
                    {
                        product[i][j] += datasetA[i][k] * datasetB[k][j];
                    }
                }
            });
            // Console.WriteLine("Multiplying Matrices...success!");

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
    }
}