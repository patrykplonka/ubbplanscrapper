using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Interactions;
using Newtonsoft.Json;
using System.IO;
using System.Threading;

class Program
{
    static IWebDriver? driver;
    static WebDriverWait? wait;
    static List<Dictionary<string, string>> results = new List<Dictionary<string, string>>();
    static HashSet<string> processedDeptIds = new HashSet<string>();

    static readonly Dictionary<string, string> typeMapping = new Dictionary<string, string>
    {
        { "lek", "lektorat" }, { "wyk", "wykład" }, { "ćw", "ćwiczenia" },
        { "proj", "projektowanie" }, { "lab", "laboratorium" }, { "wf", "ćwiczenia" },
        { "wr", "warsztaty" }, { "konw", "konwersatorium" }, { "sem", "seminarium" },
        { "pnj", "Praktyczna Nauka Języka" }
    };

    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("Uruchamianie ChromeDriver...");
            var options = new ChromeOptions();
            // options.AddArgument("--headless"); // Wyłączone dla debugowania
            options.AddArgument("--disable-gpu");
            options.AddArgument("--no-sandbox");
            driver = new ChromeDriver(options);
            wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60)); // Zwiększony timeout

            Console.WriteLine("Nawigacja do strony...");
            driver.Navigate().GoToUrl("https://plany.ubb.edu.pl/left_menu.php?type=2");
            Thread.Sleep(2000);
            Console.WriteLine("Strona załadowana, rozpoczynanie scrapowania...");

            var faculties = new List<(string facultyId, string facultyName, string branchParam, string[] deptIds)>
            {
                ("6179", "Jednostki Międzywydziałowe", "0", new[] { "6196", "6197" }),
                ("6168", "Wydział Budowy Maszyn i Informatyki", "0", new[] { "6180", "6174", "6175", "6176", "6181", "30847" }),
                ("6171", "Wydział Humanistyczno-Społeczny", "0", new[] { "6193", "76335", "6192", "150120" }),
                ("6170", "Wydział Inżynierii Materiałów, Budownictwa i Środowiska", "0", new[] { "150424" }),
                ("6178", "Wydział Nauk o Zdrowiu", "0", new[] { "76336", "76337", "76338", "76339" }),
                ("6169", "Wydział Zarządzania i Transportu", "0", new[] { "6184", "6185", "6188", "52698", "52699" })
            };

            foreach (var faculty in faculties)
            {
                Console.WriteLine($"Przetwarzanie wydziału: {faculty.facultyName} (ID: {faculty.facultyId})");
                ProcessFaculty(faculty.facultyId, faculty.facultyName, faculty.branchParam, faculty.deptIds);
            }

            Console.WriteLine($"Zebrano {results.Count} rekordów");
            if (results.Count > 0)
            {
                var jsonData = TransformToJson(results);
                File.WriteAllText("PLAN.json", JsonConvert.SerializeObject(jsonData, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                Console.WriteLine("Zapisano dane do PLAN.json");
            }
            else
            {
                Console.WriteLine("Brak danych do zapisania!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd główny: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            Console.WriteLine("Zamykanie przeglądarki...");
            driver?.Quit();
        }
    }

    static string GetSubjectType(string divText)
    {
        var match = Regex.Match(divText ?? "", @"<img[^>]*id=""arrow_course_\d+""[^>]*>(.*?)<br>", RegexOptions.Singleline);
        if (match.Success)
        {
            string text = match.Groups[1].Value.Trim().ToLower();
            return typeMapping.FirstOrDefault(kvp => text.Contains(kvp.Key)).Value ?? "nieznany";
        }
        return "nieznany";
    }

    static bool EntryExists(string dept, string coord, string subj, string subjType)
    {
        return results.Any(entry =>
            entry["Prowadzący"] == coord &&
            entry["Przedmiot"] == subj &&
            entry["Typ"] == subjType);
    }

    static void ProcessDepartment(string deptId, string facultyName, string facultyId, string branchParam)
    {
        try
        {
            Console.WriteLine($"Rozpoczynanie przetwarzania katedry {deptId}");
            EnsureFacultyExpanded(facultyId, facultyName);

            IWebElement? plusik = null;
            try
            {
                plusik = wait!.Until(d =>
                {
                    var element = d.FindElement(By.Id($"img_{deptId}"));
                    ((IJavaScriptExecutor)driver!).ExecuteScript("arguments[0].scrollIntoView(true);", element);
                    return element.Displayed ? element : null;
                });
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine($"Nie znaleziono elementu img_{deptId}. Katedra może nie istnieć lub strona nie jest w pełni załadowana.");
                processedDeptIds.Add(deptId);
                return;
            }

            string deptName = $"Katedra {deptId}";

            if (plusik.GetAttribute("src")?.Contains("plus.gif") == true)
            {
                ExpandDepartment(plusik, deptId, branchParam, facultyId);
            }

            var divDept = wait!.Until(d => d.FindElement(By.Id($"div_{deptId}")));
            var coordinatorLinks = divDept.FindElements(By.XPath(".//a[contains(@href, 'type=10')]"));
            if (!coordinatorLinks.Any())
            {
                coordinatorLinks = divDept.FindElements(By.TagName("a"));
            }

            var coordinators = coordinatorLinks
                .Where(link => !string.IsNullOrWhiteSpace(link.Text))
                .Select(link => (Name: link.Text.Trim(), Url: link.GetAttribute("href")))
                .Distinct()
                .ToList();

            Console.WriteLine($"Znaleziono {coordinators.Count} prowadzących dla katedry {deptId}");
            foreach (var coordinator in coordinators)
            {
                ProcessCoordinator(coordinator, deptName, facultyId);
            }

            processedDeptIds.Add(deptId);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Błąd w ProcessDepartment (deptId: {deptId}): {e.Message}\n{e.StackTrace}");
            processedDeptIds.Add(deptId);
        }
    }

    static void EnsureFacultyExpanded(string facultyId, string facultyName)
    {
        int attempts = 0;
        while (attempts < 3)
        {
            try
            {
                Console.WriteLine($"Próba rozwinięcia wydziału {facultyId}, próba {attempts + 1}");
                ((IJavaScriptExecutor)driver!).ExecuteScript($"branch(2,{facultyId},0,'{facultyName}');");
                wait!.Until(d => d.FindElement(By.Id(facultyId)).Displayed && d.FindElement(By.Id(facultyId)).Enabled);
                Thread.Sleep(2000);
                Console.WriteLine($"Wydział {facultyId} rozwinięty");
                return;
            }
            catch (Exception ex)
            {
                attempts++;
                Console.WriteLine($"Błąd przy rozwijaniu wydziału {facultyId}: {ex.Message}");
                Thread.Sleep(2000);
            }
        }
        throw new Exception($"Nie udało się rozwinąć wydziału {facultyId} po 3 próbach");
    }

    static void ExpandDepartment(IWebElement plusik, string deptId, string branchParam, string facultyId)
    {
        int attempts = 0;
        while (attempts < 3)
        {
            try
            {
                Console.WriteLine($"Próba rozwinięcia katedry {deptId}, próba {attempts + 1}");
                new Actions(driver!).MoveToElement(plusik).Click().Perform();
                wait!.Until(d => d.FindElement(By.Id($"div_{deptId}")).Displayed);
                Thread.Sleep(2000);
                Console.WriteLine($"Katedra {deptId} rozwinięta");
                return;
            }
            catch (Exception ex)
            {
                attempts++;
                Console.WriteLine($"Błąd przy rozwijaniu katedry {deptId}: {ex.Message}");
                ((IJavaScriptExecutor)driver!).ExecuteScript($"get_left_tree_branch('{deptId}', 'img_{deptId}', 'div_{deptId}', '2', '{branchParam}');");
                Thread.Sleep(2000);
            }
        }
        throw new Exception($"Nie udało się rozwinąć katedry {deptId} po 3 próbach");
    }

    static void ProcessCoordinator((string Name, string Url) coordinator, string deptName, string facultyId)
    {
        driver!.Navigate().GoToUrl(coordinator.Url);
        Thread.Sleep(2000);

        if (driver.FindElements(By.Id("legend")).Count == 0)
        {
            Console.WriteLine($"Brak legendy dla prowadzącego {coordinator.Name}");
            return;
        }

        try
        {
            var legend = wait!.Until(d => d.FindElement(By.Id("legend")));
            var dataDiv = legend.FindElement(By.ClassName("data"));
            string legendText = dataDiv.GetAttribute("innerHTML");

            var subjectPattern = new Regex(@"<strong>(.*?)</strong> - (.*?)(?:, występowanie|\s*<br|\s*<hr)", RegexOptions.Singleline);
            var subjects = subjectPattern.Matches(legendText).Cast<Match>().ToList();
            var courseDivs = driver.FindElements(By.XPath("//div[starts-with(@id, 'course_')]"));

            if (subjects.Any())
            {
                foreach (var match in subjects)
                {
                    string subjectCode = match.Groups[1].Value;
                    string subjectName = match.Groups[2].Value.Trim();
                    foreach (var div in courseDivs)
                    {
                        string divText = div.GetAttribute("innerHTML");
                        if (divText.Contains(subjectCode))
                        {
                            string subjectType = GetSubjectType(divText);
                            if (!EntryExists(deptName, coordinator.Name, subjectName, subjectType))
                            {
                                results.Add(new Dictionary<string, string>
                                {
                                    { "Katedra", deptName },
                                    { "Prowadzący", coordinator.Name },
                                    { "Przedmiot", subjectName },
                                    { "Typ", subjectType }
                                });
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var div in courseDivs)
                {
                    string divText = div.GetAttribute("innerHTML") ?? "";
                    string subjectName = divText.Split(new[] { "<br>" }, StringSplitOptions.None)[0].Trim();
                    string subjectType = GetSubjectType(divText);
                    if (!EntryExists(deptName, coordinator.Name, subjectName, subjectType))
                    {
                        results.Add(new Dictionary<string, string>
                        {
                            { "Katedra", deptName },
                            { "Prowadzący", coordinator.Name },
                            { "Przedmiot", subjectName },
                            { "Typ", subjectType }
                        });
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Błąd w ProcessCoordinator dla {coordinator.Name}: {e.Message}");
        }

        driver.Navigate().GoToUrl("https://plany.ubb.edu.pl/left_menu.php?type=2");
        wait!.Until(d => d.FindElement(By.Id(facultyId)));
    }

    static void ProcessFaculty(string facultyId, string facultyName, string branchParam, string[] deptIds)
    {
        try
        {
            driver!.Navigate().GoToUrl("https://plany.ubb.edu.pl/left_menu.php?type=2");
            EnsureFacultyExpanded(facultyId, facultyName);

            foreach (var deptId in deptIds)
            {
                if (!processedDeptIds.Contains(deptId))
                {
                    ProcessDepartment(deptId, facultyName, facultyId, branchParam);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Błąd w ProcessFaculty: {e.Message}");
        }
    }

    static Dictionary<string, Dictionary<string, Dictionary<string, object>>> TransformToJson(List<Dictionary<string, string>> results)
    {
        var jsonData = new Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>>();
        foreach (var entry in results)
        {
            string dept = entry["Katedra"];
            string subject = entry["Przedmiot"];
            string subjectType = entry["Typ"];
            string coordinator = entry["Prowadzący"];

            if (!jsonData.ContainsKey(dept))
                jsonData[dept] = new Dictionary<string, Dictionary<string, List<string>>>();
            if (!jsonData[dept].ContainsKey(subject))
                jsonData[dept][subject] = new Dictionary<string, List<string>>();
            if (!jsonData[dept][subject].ContainsKey(subjectType))
                jsonData[dept][subject][subjectType] = new List<string>();

            if (!jsonData[dept][subject][subjectType].Contains(coordinator))
                jsonData[dept][subject][subjectType].Add(coordinator);
        }

        var finalJson = new Dictionary<string, Dictionary<string, Dictionary<string, object>>>();
        foreach (var dept in jsonData.Keys)
        {
            finalJson[dept] = new Dictionary<string, Dictionary<string, object>>();
            foreach (var subject in jsonData[dept].Keys)
            {
                if (jsonData[dept][subject].ContainsKey("wykład"))
                {
                    finalJson[dept][subject] = new Dictionary<string, object>
                    {
                        { "Typ", "wykład" },
                        { "Prowadzący", jsonData[dept][subject]["wykład"] }
                    };
                }
                else
                {
                    var firstType = jsonData[dept][subject].Keys.First();
                    finalJson[dept][subject] = new Dictionary<string, object>
                    {
                        { "Typ", firstType },
                        { "Prowadzący", jsonData[dept][subject][firstType] }
                    };
                }
            }
        }
        return finalJson;
    }
}