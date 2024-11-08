using System.Security.Cryptography;


string file1 = @"";
string file2 = @"";

if(Test1.CalculateFileHash(file1) == Test1.CalculateFileHash(file2))
{
    Console.WriteLine("True");
}

public static class Test1
{
    public static string CalculateFileHash(string filePath)
    {
        using (var sha256 = SHA256.Create())
        {
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hashBytes = sha256.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}