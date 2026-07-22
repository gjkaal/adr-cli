using McpCore;

using System;
using System.Threading;

namespace Adr.Cli.Services
{
    public class StdOutService : IStdOut
    {
        private static readonly Lock Lock = new();
        private static bool muted = false;
        public bool Muted => muted;

        public void Mute()
        {
            if (muted)
            {
                return;
            }

            lock (Lock)
            {
                muted = true;
            }
        }

        public void UnMute()
        {
            if (!muted)
            {
                return;
            }

            lock (Lock)
            {
                muted = false;
            }
        }

        public void Write(string text)
        {
            if (muted)
            {
                return;
            }

            Console.Write(text);
        }

        public void Write(Response response)
        {
            if (muted)
            {
                return;
            }

            Console.WriteLine(response.Success ? "> OK" : "> FAILED");
            Console.WriteLine(response.Message);
        }

        public void WriteLine(string text)
        {
            if (muted)
            {
                return;
            }

            Console.WriteLine(text);
        }
    }
}
