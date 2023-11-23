using Microsoft.Playwright;
using System.Reflection;

namespace Bingto
{
    internal class Program
    {
        static readonly string? version = Assembly.GetExecutingAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        static readonly string[] installArgs = ["install", "chromium", "webkit"];
        static readonly Random random = new();

        static async Task Main(string[] args)
        {
            Console.Title = $"Bingto";
            if (version != null)
            {
                Console.WriteLine($"Bingto v{version} - https://github.com/teppyboy/bingto-cs");
            }
            else
            {
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
            Console.WriteLine("Loading word list...");
            WordList wordList = await WordList.Init();
            await StartPC(playwright, wordList);
            await StartMobile(playwright, wordList);
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

        /// <summary>
        /// Open the mobile drawer.
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        static async Task MOpenDrawer(IPage page)
        {
            try
            {
                await page.ClickAsync("#mHamburger", new PageClickOptions()
                {
                    Timeout = 5000,
                });
            }
            catch (PlaywrightException)
            {
                Console.WriteLine("Failed to open drawer.");
            }
        }

        static async Task<int> GetScore(IPage page, bool mobile)
        {
            if (mobile)
            {
                await MOpenDrawer(page);
                await Wait(500, 1500);
                for (int i = 0; i < 3; i++)
                {
                    Console.WriteLine("Getting score (mobile)...");
                    try
                    {
                        return int.Parse(await page.InnerTextAsync("#fly_id_rc", new()
                        {
                            Timeout = 3000,
                        }));
                    }
                    catch (Exception ex) when (
                        ex is FormatException
                        || ex is PlaywrightException
                    )
                    {   
                        // TODO: Handle timeout properly
                        Console.WriteLine("Failed to get score, waiting...");
                        await MOpenDrawer(page);
                        await Wait(500, 1500);
                    }
                }
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

        static async Task Search(IPage page, WordList wordList, bool mobile = false)
        {
            var prevScore = -1;
            var sameScoreCount = 0;
            for (int i = 0; i < 50; i++)
            {
                Console.WriteLine($"Search attempt {i + 1}/50...");
                var wordLength = random.Next(2, 10);
                var words = wordList.GetRandomWords(wordLength);
                Console.WriteLine($"Words: {string.Join(" ", words)}");
                if (mobile)
                {
                    if (i == 0)
                    {
                        Console.WriteLine("Simulating typing (first search) on mobile...");
                        await page.ClickAsync("#HBleft");
                        await Wait(1000, 2000);
                        await page.ClickAsync("#sb_form_c");
                        await Wait(2000, 3000);
                        await page.Keyboard.TypeAsync(string.Join(" ", words), new KeyboardTypeOptions()
                        {
                            Delay = 50,
                        });
                        await Wait(1000, 2000);
                        await page.Keyboard.PressAsync("Enter");
                    }
                    else
                    {
                        Console.WriteLine("Simulating typing on mobile...");
                        try
                        {
                            await page.ClickAsync("#HBleft", new PageClickOptions()
                            {
                                Timeout = 1000,
                            });
                        }
                        catch (PlaywrightException)
                        {
                            Console.WriteLine("Drawer already closed.");
                        }
                        await SearchQuery(page, string.Join(" ", words));
                    }
                    await Wait(500, 1000);
                    await page.Locator(".tilk").First.ClickAsync();
                    await Wait(1000, 2000);
                    await page.GoBackAsync();
                    await Wait(500, 1000);
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
        static async Task StartPC(IPlaywright playwright, WordList wordlist, bool silent = false, bool forceChromium = false)
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
            await Search(page, wordlist, false);
            var cookies = await context.StorageStateAsync();
            File.WriteAllText("cookies.json", cookies);
        }

        static async Task StartMobile(IPlaywright playwright, WordList wordlist, bool silent = false, bool noWebkit = false, bool forceChromium = false, bool realViewport = false, bool usePcProfile = false)
        {
            var profile = playwright.Devices["iPhone 13 Pro Max"];
            var userAgent = Constants.MOBILE_UA_TEMPLATE.Replace("{IOS_VERSION}", Constants.VALID_IOS_VERSIONS[random.Next(0, Constants.VALID_IOS_VERSIONS.Length)]);
            Console.WriteLine($"Using user agent: {userAgent}");
            Console.WriteLine("Monkey-patching WebKit user agent...");
            profile.UserAgent = userAgent;
            // Use the cookies file as the storage state path.
            profile.StorageStatePath = "cookies.json";
            // var cookies = await LoadCookies("cookies.json");
            await using var browser = await playwright.Webkit.LaunchAsync(new BrowserTypeLaunchOptions()
            {
                Headless = silent,
            });
            await using var context = await browser.NewContextAsync(profile);
            var page = await context.NewPageAsync();
            await page.GotoAsync("https://www.bing.com/");
            await Wait(2000, 3000);
            if (await GetScore(page, true) == -1)
            {
                Console.WriteLine("Clicking the 'Login' button...");
                try
                {
                    await page.ClickAsync("#hb_s", new PageClickOptions()
                    {
                        Timeout = 1000,
                    });
                }
                catch (PlaywrightException)
                {
                    Console.WriteLine("Failed to click the 'Login' button, assuming we're logged in.");
                }
            }
            await Wait(3000, 5000);
            await CheckSession(page);
            await Search(page, wordlist, true);
            var cookies = await context.StorageStateAsync();
            File.WriteAllText("cookies.json", cookies);
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
                await using var browser = await LaunchBrowser(playwright);
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
