using Microsoft.Playwright;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bingto
{
    internal class Program
    {
        static readonly string? version = Assembly.GetExecutingAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        internal static readonly string[] installArgs = ["install", "chromium", "webkit"];
        static readonly Random random = new();

        static async Task Main(string[] args)
        {
            if (version != null)
            {
                Console.Title = $"Bingto v{version}";
                Console.WriteLine($"Bingto v{version} - https://github.com/teppyboy/bingto-cs");
            }
            else
            {
                Console.Title = "Bingto";
                Console.WriteLine("Bingto - https://github.com/teppyboy/bingto-cs");
            }
            Console.WriteLine("Experimental version of Bingto in C#, use at your own risk.");
            using var playwright = await Playwright.CreateAsync();
            if (!CheckBrowsers(playwright))
            {
                InstallBrowsers();
            }
            string? cookiesFile = Path.GetDirectoryName(System.AppContext.BaseDirectory) + "/cookies.json";
            if (!Path.Exists(cookiesFile))
            {
                Console.WriteLine("Cookies not found, initiating the login process...");
                await Login(playwright);
            }
            await StartPC(playwright);
        }

        /// <summary>
        /// Wait for a random amount of time between min and max.
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        static async Task Wait(int min, int max)
        {
            await Task.Delay(random.Next(min, max));
        }

        /// <summary>
        /// Launch Edge/Chromium browser.
        /// </summary>
        /// <param name="playwright"></param>
        /// <param name="silent"></param>
        /// <param name="forceChromium"></param>
        /// <returns></returns>
        static async Task<IBrowser> LaunchBrowser(IPlaywright playwright, bool silent = false, bool forceChromium = false)
        {
            var options = new BrowserTypeLaunchOptions
            {
                Headless = silent,
            };
            if (!forceChromium)
            {
                options.Channel = "msedge";
            }
            try
            {
                return await playwright.Chromium.LaunchAsync(options);
            }
            catch (PlaywrightException)
            {
                return await playwright.Chromium.LaunchAsync(new()
                {
                    Headless = silent,
                });
            }
        }   

        static async Task SearchQuery(IPage page, string query)
        {
            var searchBox = await page.QuerySelectorAsync("#sb_form_q");
            if (searchBox == null)
            {
                Console.WriteLine("Search box not found, wtf?");
                return;
            }
            await searchBox.ClickAsync();
            await Wait(2000, 3000);
            await searchBox.FillAsync(query);
            await Wait(1000, 2000);
            await page.Keyboard.PressAsync("Enter");
        }

        static async Task<int> GetScore(IPage page, bool mobile)
        {
            if (mobile)
            {

            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    Console.WriteLine("Getting score...");
                    try
                    {
                        return int.Parse(await page.InnerTextAsync("#id_rc", new()
                        {
                            Timeout = 3000,
                        }));
                    }
                    catch (Exception ex) when (
                        ex is FormatException
                        || ex is PlaywrightException
                    )
                    {
                        Console.WriteLine("Failed to get score, waiting...");
                        await Wait(500, 1500);
                    }
                }
            }
            return -1;
        }

        static async Task Search(IPage page, bool mobile = false)
        {
            var prevScore = -1;
            var sameScoreCount = 0;
            // Initialize wordlist
            var wordList = await WordList.Init();
            for (int i = 0; i < 50; i++)
            {
                Console.WriteLine($"Search attempt {i + 1}/50...");
                var wordLength = random.Next(2, 10);
                var words = wordList.GetRandomWords(wordLength);
                Console.WriteLine($"Words: {string.Join(" ", words)}");
                if (mobile)
                {
                    // TODO: Implement mobile search
                }
                else
                {
                    if (i == 0)
                    {
                        var query = string.Join("+", words);
                        await page.GotoAsync($"https://www.bing.com/search?q={query}&form=QBLH");
                    }
                    else
                    {
                        Console.WriteLine("Simulating typing on PC...");
                        await SearchQuery(page, string.Join(" ", words));
                    }
                }
                await Wait(3000, 4000);
                var curScore = await GetScore(page, mobile);
                Console.WriteLine($"Score (current / previous): {curScore} / {prevScore}");
                if (curScore == -1)
                {
                    Console.WriteLine("Failed to get score, continuing...");
                    continue;
                }
                if (curScore == prevScore)
                {
                    sameScoreCount++;
                    Console.WriteLine($"Same score count: {sameScoreCount}");
                    if (sameScoreCount >= 3)
                    {
                        Console.WriteLine("Score did not change 3 times, probably we searched enough.");
                        Console.WriteLine("If the score isn't full, please report this issue on GitHub.");
                        break;
                    }
                }
                else
                {
                    sameScoreCount = 0;
                    prevScore = curScore;
                }
            }
            Console.WriteLine("Search completed.");
        }

        // [RequiresUnreferencedCode("Calls Bingto.Program.LoadCookies(String)")]
        // [RequiresDynamicCode("Calls Bingto.Program.LoadCookies(String)")]
        static async Task StartPC(IPlaywright playwright, bool silent = false, bool forceChromium = false)
        {
            var profile = playwright.Devices["Desktop Edge"];
            // Use the cookies file as the storage state path.
            profile.StorageStatePath = "cookies.json";
            // var cookies = await LoadCookies("cookies.json");
            await using var browser = await LaunchBrowser(playwright, silent, forceChromium);
            await using var context = await browser.NewContextAsync(profile);
            var page = await context.NewPageAsync();
            await page.GotoAsync("https://www.bing.com/");
            await Wait(2000, 3000);
            await page.ClickAsync("#id_l");
            await Wait(1000, 2000);
            await CheckSession(page);
            await Search(page, false);
        }

        static async Task CheckSession(IPage page)
        {
            var url = await page.EvaluateAsync<string>("location.href");
            if (url.Contains("https%3a%2f%2fwww.bing.com%2fsecure%2fPassport.aspx") && url.StartsWith("https://login.live.com/login.srf"))
            {
                Console.WriteLine("Session expired, please delete cookies.json and try again.");
                Environment.Exit(2);
            }
        }

        /// <summary>
        /// Login to Bing and save the cookies.
        /// </summary>
        static async Task Login(IPlaywright playwright)
        {
            Console.WriteLine("This will open a new browser, please login within 5 minutes inside that browser.");
            Console.WriteLine("After logging in, the browser will be automatically closed.");
            try
            {
                // We use temporary data directory because the persistent context only work for the current browser type
                // e.g. Chromium as we're using now, and if we want to use Edge/WebKit, we need to login again.
                await using var browser = await playwright.Chromium.LaunchAsync(new() 
                {
                    Headless = false,
                });
                var profile = playwright.Devices["Desktop Edge"];
                await using var context = await browser.NewContextAsync(profile);
                var page = await context.NewPageAsync();
                await page.GotoAsync("https://login.live.com/login.srf");
                await page.WaitForURLAsync("https://account.microsoft.com/*", new()
                {
                    Timeout = 60 * 5 * 1000,
                });
                Console.WriteLine("Login successful, saving cookies...");
                var cookies = await context.StorageStateAsync();
                File.WriteAllText("cookies.json", cookies);
                Console.WriteLine("Cookies saved to cookies.json");
                Console.WriteLine("===========================================");
                Console.WriteLine("DO NOT SHARE THE COOKIES FILE WITH ANYONE!");
                Console.WriteLine(
                    "IT CONTAINS YOUR LOGIN INFORMATION, AND CAN BE USED TO ACCESS YOUR ACCOUNT!"
                );
                Console.WriteLine("===========================================");
            }
            catch (PlaywrightException e)
            {
                Console.WriteLine("Error: " + e.Message);
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Install browsers required by Playwright.
        /// </summary>
        static void InstallBrowsers()
        {
            Console.WriteLine("Installing browsers...");
            Microsoft.Playwright.Program.Main(installArgs);
        }

        /// <summary>
        /// Check if the required browsers are installed.
        /// </summary>
        static bool CheckBrowsers(IPlaywright playwright)
        {
            var chromiumPath = playwright.Chromium.ExecutablePath;
            var webkitPath = playwright.Webkit.ExecutablePath;
            if (chromiumPath == null || webkitPath == null)
                return false;
            return true;
        }
    }
}
