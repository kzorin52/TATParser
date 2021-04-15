using Fb2.Document;
using Fb2.Document.Models;
using Fb2.Document.Models.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

namespace TATParser
{
    class Program
    {
        static void Main(string[] args)
        {

            Console.WriteLine(GetText());
            Console.ReadKey();

            Fb2Document fb2Document = Fb2Document.CreateDocument();
            var options = new ChromeOptions
            {
                //BinaryLocation = @"C:\Program Files\Google\Chrome Beta\Application\chrome.exe"
            };
            options.AddArgument("--log-level=3");
            options.AddArgument("--disable-logging");
            //options.AddArgument("--headless");

            var driver = new ChromeDriver(options)
            {
                Url = "https://author.today/"
            };

            if (!File.Exists("cookies"))
            {
                driver.FindElement(By.LinkText("Войти")).Click();
                input("Войдите в свой аккаунт, и нажмите *ENTER*");
                SaveCookies(driver.Manage().Cookies.AllCookies.ToArray());
            }
            else
            {
                driver.Manage().Cookies.DeleteAllCookies();
                foreach (var cookie in LoadCookies())
                    driver.Manage().Cookies.AddCookie(cookie);
                driver.Navigate().Refresh();
                SaveCookies(driver.Manage().Cookies.AllCookies.ToArray());
            }

            var bookId = input("Введите ссылку на книгу (https://author.today/work/119568)")
                .Replace("https://", "")
                .Replace("http://", "")
                .Split('/')[2]
                .intParse();

            driver.Navigate().GoToUrl($"https://author.today/reader/{bookId}");
        again:
            Thread.Sleep(500);

            var fragments = driver.FindElements(By.XPath("//div[@class='text-container']//p"));
            foreach (var fragment in fragments)
            {
                Console.WriteLine($"{fragment.Text}\r\n");

                var textItem = new TextItem();
                textItem.Load(new XText(fragment.Text));

                var p = new Paragraph();
                p.Content.Add(textItem);

                var section = new BodySection();
                section.Content.Add(p);

                var body = new BookBody();
                body.Content.Add(section);

                fb2Document.Book.Content.Add(body);
            }

            File.WriteAllText("book.fb2", fb2Document.ToXmlString());

            try
            {
                driver.FindElement(By.XPath("//li[@class='next']//span[1]")).Click();
                goto again;
            }
            catch { }

            Console.ReadKey();
            driver.Close();
            driver.Quit();
            Environment.Exit(0);
        }


        public static string GetText(int bookId = 119568, int Id = 962097)
        {
            var client = new HttpClient();

            var response = client.GetAsync($"https://author.today/reader/{bookId}/chapter?id={Id}").Result;
            var secret = response.Headers.GetValues("Reader-Secret").FirstOrDefault();
            var key = string.Join("", secret.Reverse()) + "@_@";

            var text = JsonConvert.DeserializeObject<Response>(response.Content.ReadAsStringAsync().Result).Data.Text;

            var endText = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
                endText.Append((char)(text[i] ^ key[i % key.Length]));

            return endText.ToString();
        }

        static void SaveCookies(Cookie[] cookies)
        {
            var filename = "cookies";
            List<string> cookiesJsons = new List<string>();
            foreach (var cookie in cookies)
                cookiesJsons.Add(cookie.GetString());

            File.WriteAllLines(filename, cookiesJsons);
        }
        static Cookie[] LoadCookies()
        {
            var filename = "cookies";
            List<string> cookiesJsons = File.ReadAllLines(filename).ToList();
            List<Cookie> cookies = new List<Cookie>();
            foreach (var cookiesJson in cookiesJsons)
                cookies.Add(cookiesJson.GetCookie());

            return cookies.ToArray();
        }

        #region Так удобнее

        /// <summary>
        /// Взял из питона, но так рил удобнее. Метод для ввода текста, 
        /// но с выводом другого текста
        /// </summary>
        /// <param name="text">Текст для вывода</param>
        /// <returns>Введённую строку</returns>
        static string input(string text = "")
        {
            if (text != "")
                Console.WriteLine(text);
            return Console.ReadLine();
        }

        #endregion
    }

    public class Response
    {
        public ResponseData Data;
    }

    public class ResponseData
    {
        public string Text;
    }

    #region Расширения

    /// <summary>
    /// Класс расширений
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Преобразует строку в число (по-моему, так удобнее)
        /// </summary>
        /// <param name="str">Строка</param>
        /// <returns>Число</returns>
        public static int intParse(this string str) => int.Parse(str);
        public static string GetString(this Cookie cookie) => JsonConvert.SerializeObject(cookie);
        public static Cookie GetCookie(this string str)
        {
            var obj = JObject.Parse(str);
            DateTime? expiry = null;

            try
            {
                expiry = obj["Expiry"].ToObject<DateTime>();
            }
            catch { }

            Cookie newCookie = new Cookie(
                obj["Name"].ToString(),
                obj["Value"].ToString(),
                obj["Domain"].ToString(),
                obj["Path"].ToString(),
                expiry);

            return newCookie;
        }
    }

    #endregion
}
