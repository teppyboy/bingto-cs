using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Bingto
{
    internal class WordList
    {
        private const string WORD_LIST_URL = "https://raw.githubusercontent.com/dwyl/english-words/master/words_alpha.txt";
        private const string WORD_LIST_FILE = "words_alpha.txt";
        readonly string[] list;

        private WordList(string[] wordList)
        {
            list = wordList;
        }

        public static async Task<WordList> Init()
        {
            string? content = null;
            if (!File.Exists(WORD_LIST_FILE))
            {
                Console.WriteLine("Word list not found, downloading...");
                using var client = new HttpClient();
                content = await client.GetStringAsync(WORD_LIST_URL);
                await File.WriteAllTextAsync(WORD_LIST_FILE, content);
            }
            content ??= await File.ReadAllTextAsync(WORD_LIST_FILE);
            return new WordList(content.Split("\n"));
        }

        public string GetRandomWord()
        {
            var word = list[new Random().Next(0, list.Length)];
            return word[..(word.Length - 1)];
        }

        public string[] GetRandomWords(int count)
        {
            var words = new List<string>();
            for (int i = 0; i < count; i++)
            {
                words.Add(GetRandomWord());
            }
            return [.. words];
        }
    }
}
