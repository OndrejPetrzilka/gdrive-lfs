namespace GoogleDriveLFS
{
    internal class Program
    {
        static readonly string FileName = "c:\\Data\\Projects\\drive-test3\\log.txt";

        static void Main(string[] args)
        {
            File.WriteAllLines(FileName, args);
            File.AppendAllText(FileName, "\r\n\r\n");
            while (true)
            {
                string? str = Console.ReadLine();
                if (str == null)
                    return;

                File.AppendAllLines(FileName, new string[] { str });
            }
        }
    }
}