using System;

namespace adr.Services
{
    public interface IStdOut
    {
        void Write(string text);
        void WriteLine(string text);
    }

    public class StdOutService : IStdOut
    {
        public void Write(string text)
        {
            Console.Write(text);
        }

        public void WriteLine(string text)
        {
            Console.WriteLine(text);
        }
    }
}
