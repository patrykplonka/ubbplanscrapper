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
    static HashSet<string> processedCoordinators = new HashSet<string>();
    static readonly Dictionary<string, string> typeMapping = new Dictionary<string, string>
    {
        { "lek", "lektorat" }, { "wyk", "wykład" }, { "ćw", "ćwiczenia" },
        { "proj", "projektowanie" }, { "lab", "laboratorium" }, { "wf", "ćwiczenia" },
        { "wr", "warsztaty" }, { "konw", "konwersatorium" }, { "sem", "seminarium" },
        { "pnj", "Praktyczna Nauka Języka" }
    };

    static readonly Dictionary<string, string> studyModeMapping = new Dictionary<string, string>
    {
        { "S", "Stacjonarne" },
        { "NZ", "Niestacjonarne Zaoczne" },
        { "NW", "Niestacjonarne Wieczorowe" }
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
            wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));

            Console.WriteLine("Nawigacja do strony...");
            driver.Navigate().GoToUrl("https://plany.ubb.edu.pl/left_menu.php?type=2");
            Thread.Sleep(3000); // Dłuższe oczekiwanie na pełne załadowanie strony
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
        // Rozszerzony wzorzec, aby lepiej wyodrębnić typ zajęć
        var match = Regex.Match(divText ?? "", @"<img[^>]*id=""arrow_course_\d+""[^>]*>(.*?)<br>", RegexOptions.Singleline);
        if (match.Success)
        {
            string text = match.Groups[1].Value.Trim().ToLower();

            foreach (var kvp in typeMapping)
            {
                if (text.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }
        }

        // Alternatywny sposób szukania, jeśli pierwszy nie zadziałał
        foreach (var kvp in typeMapping)
        {
            if (divText.ToLower().Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }

        return "nieznany";
    }

    static string GetStudyMode(string text)
    {
        foreach (var mode in studyModeMapping.Keys)
        {
            if (Regex.IsMatch(text, $@"\b{mode}\b"))
            {
                return studyModeMapping[mode];
            }
        }

        // Dodatkowe sprawdzenie dla przypadków, gdy tryb studiów jest zapisany inaczej
        if (text.Contains("stacjonar"))
            return "Stacjonarne";
        if (text.Contains("niestacjonar") && text.Contains("zaocz"))
            return "Niestacjonarne Zaoczne";
        if (text.Contains("niestacjonar") && text.Contains("wieczor"))
            return "Niestacjonarne Wieczorowe";

        return "nieznany";
    }
    static bool EntryExists(Dictionary<string, string> entry)
    {
        return results.Any(existingEntry =>
            existingEntry["Katedra"] == entry["Katedra"] &&
            existingEntry["Przedmiot"] == entry["Przedmiot"] &&
            existingEntry["Typ"] == entry["Typ"] &&
            existingEntry["Tryb studiów"] == entry["Tryb studiów"] &&
            existingEntry["Prowadzący"] == entry["Prowadzący"]);
    }

    static void ProcessDepartment(string deptId, string facultyName, string facultyId, string branchParam)
    {
        try
        {
            Console.WriteLine($"Rozpoczynanie przetwarzania katedry {deptId}");
            EnsureFacultyExpanded(facultyId, facultyName);

            try
            {
                // Próba znalezienia ikony rozwijania dla katedry
                IWebElement? plusik = wait!.Until(d =>
                {
                    try
                    {
                        var element = d.FindElement(By.Id($"img_{deptId}"));
                        ((IJavaScriptExecutor)driver!).ExecuteScript("arguments[0].scrollIntoView(true);", element);
                        return element.Displayed ? element : null;
                    }
                    catch
                    {
                        return null;
                    }
                });

                string deptName = GetDepartmentName(deptId, facultyName);

                if (plusik != null && plusik.GetAttribute("src")?.Contains("plus.gif") == true)
                {
                    ExpandDepartment(plusik, deptId, branchParam, facultyId);
                }

                // Próba znalezienia div katedry po rozwinięciu
                var divDept = wait!.Until(d => {
                    try
                    {
                        return d.FindElement(By.Id($"div_{deptId}"));
                    }
                    catch
                    {
                        return null;
                    }
                });

                if (divDept != null)
                {
                    // Pobieranie wszystkich linków prowadzących w obrębie katedry
                    var allLinks = divDept.FindElements(By.TagName("a")).ToList();

                    // Filtrowanie i usunięcie duplikatów
                    var coordinators = allLinks
                        .Where(link =>
                            !string.IsNullOrWhiteSpace(link.Text) &&
                            (link.GetAttribute("href")?.Contains("type=10") == true ||
                             link.GetAttribute("href")?.Contains("schedule") == true))
                        .Select(link => (Name: link.Text.Trim(), Url: link.GetAttribute("href")))
                        .GroupBy(item => item.Name)
                        .Select(g => g.First())
                        .ToList();

                    Console.WriteLine($"Znaleziono {coordinators.Count} prowadzących dla katedry {deptId}");
                    foreach (var coordinator in coordinators)
                    {
                        ProcessCoordinator(coordinator, deptName, facultyId);
                    }
                }
                else
                {
                    Console.WriteLine($"Nie znaleziono rozwiniętej katedry div_{deptId}");
                }
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine($"Nie znaleziono elementu img_{deptId}. Katedra może nie istnieć lub strona nie jest w pełni załadowana.");
            }

            processedDeptIds.Add(deptId);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Błąd w ProcessDepartment (deptId: {deptId}): {e.Message}\n{e.StackTrace}");
            processedDeptIds.Add(deptId);
        }
    }

        static string GetDepartmentName(string deptId, string facultyName)
    {
        try
        {
            // Próba znalezienia nazwy katedry na stronie
            var deptElement = driver!.FindElements(By.XPath($"//span[contains(@onclick, '{deptId}')]")).FirstOrDefault();
            if (deptElement != null)
            {
                return deptElement.Text.Trim();
            }
        }
        catch
        {
            // Ignorujemy błędy i używamy fallbacku
        }

        return $"Katedra {deptId} ({facultyName})";
    }

 static void EnsureFacultyExpanded(string facultyId, string facultyName)
    {
        int attempts = 0;
        while (attempts < 3)
        {
            try
            {
                Console.WriteLine($"Próba rozwinięcia wydziału {facultyId}, próba {attempts + 1}");

                // Sprawdzenie, czy wydział jest już rozwinięty
                try
                {
                    var facultyElement = driver!.FindElement(By.Id(facultyId));
                    var imgElement = driver.FindElement(By.Id($"img_{facultyId}"));

                    if (imgElement.GetAttribute("src")?.Contains("minus.gif") == true)
                    {
                        Console.WriteLine($"Wydział {facultyId} już rozwinięty");
                        return;
                    }
                }
                catch { }

                // Jeśli nie jest rozwinięty, próbujemy rozwinąć
                ((IJavaScriptExecutor)driver!).ExecuteScript($"branch(2,{facultyId},0,'{facultyName}');");
                Thread.Sleep(500);

                wait!.Until(d => {
                    try
                    {
                        var element = d.FindElement(By.Id(facultyId));
                        return element.Displayed && element.Enabled;
                    }
                    catch
                    {
                        return false;
                    }
                });

                Console.WriteLine($"Wydział {facultyId} rozwinięty");
                return;
            }
            catch (Exception ex)
            {
                attempts++;
                Console.WriteLine($"Błąd przy rozwijaniu wydziału {facultyId}: {ex.Message}");
                Thread.Sleep(500);
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

                // Próba kliknięcia w element
                new Actions(driver!).MoveToElement(plusik).Click().Perform();
                Thread.Sleep(500);

                // Weryfikacja, czy katedra została rozwinięta
                bool isExpanded = wait!.Until(d => {
                    try
                    {
                        var div = d.FindElement(By.Id($"div_{deptId}"));
                        return div.Displayed;
                    }
                    catch
                    {
                        return false;
                    }
                });

                if (isExpanded)
                {
                    Console.WriteLine($"Katedra {deptId} rozwinięta");
                    return;
                }

                // Próba alternatywna z JavaScript
                ((IJavaScriptExecutor)driver!).ExecuteScript(
                    $"get_left_tree_branch('{deptId}', 'img_{deptId}', 'div_{deptId}', '2', '{branchParam}');"
                );
                Thread.Sleep(500);

                isExpanded = wait!.Until(d => {
                    try
                    {
                        var div = d.FindElement(By.Id($"div_{deptId}"));
                        return div.Displayed;
                    }
                    catch
                    {
                        return false;
                    }
                });

                if (isExpanded)
                {
                    Console.WriteLine($"Katedra {deptId} rozwinięta przez JavaScript");
                    return;
                }

                attempts++;
            }
            catch (Exception ex)
            {
                attempts++;
                Console.WriteLine($"Błąd przy rozwijaniu katedry {deptId}: {ex.Message}");
                try
                {
                    ((IJavaScriptExecutor)driver!).ExecuteScript(
                        $"get_left_tree_branch('{deptId}', 'img_{deptId}', 'div_{deptId}', '2', '{branchParam}');"
                    );
                    Thread.Sleep(500);
                }
                catch { }
            }
        }
        throw new Exception($"Nie udało się rozwinąć katedry {deptId} po 3 próbach");
    }

    static void ProcessCoordinator((string Name, string Url) coordinator, string deptName, string facultyId)
    {
        try
        {
            Console.WriteLine($"Przetwarzanie prowadzącego: {coordinator.Name}");

            // Scrapowanie bieżącego tygodnia
            driver!.Navigate().GoToUrl(coordinator.Url);
            Thread.Sleep(500);
            ScrapeSchedule(coordinator.Name, deptName);

            // Pobieranie URL dla następnego tygodnia
            string currentUrl = driver.Url;
            var match = Regex.Match(currentUrl, @"w=(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                int currentW = int.Parse(match.Groups[1].Value);
                int nextW = currentW + 1;
                string nextUrl = Regex.Replace(currentUrl, @"w=\d+", $"w={nextW}", RegexOptions.IgnoreCase);

                // Scrapowanie następnego tygodnia
                driver.Navigate().GoToUrl(nextUrl);
                Thread.Sleep(500);
                ScrapeSchedule(coordinator.Name, deptName);
            }
            else
            {
                Console.WriteLine($"Nie można znaleźć parametru 'w' w URL: {currentUrl}");
            }

            // Powrót do menu lewego
            driver.Navigate().GoToUrl("https://plany.ubb.edu.pl/left_menu.php?type=2");
            Thread.Sleep(500);

            // Upewnienie się, że wydział jest rozwinięty po powrocie
            wait!.Until(d => {
                try
                {
                    return d.FindElement(By.Id(facultyId)).Displayed;
                }
                catch
                {
                    return false;
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd w ProcessCoordinator dla {coordinator.Name}: {ex.Message}");

            // Zapewniamy powrót do menu głównego nawet po błędzie
            try
            {
                driver!.Navigate().GoToUrl("https://plany.ubb.edu.pl/left_menu.php?type=2");
                Thread.Sleep(500);
            }
            catch { }
        }
    }

        static void ScrapeSchedule(string coordinatorName, string deptName)
    {
        try
        {
            // Sprawdzenie, czy istnieje legenda
            var legendExists = driver!.FindElements(By.Id("legend")).Count > 0;
            if (!legendExists)
            {
                Console.WriteLine($"Brak legendy dla prowadzącego {coordinatorName}");
                return;
            }

            var legend = wait!.Until(d => d.FindElement(By.Id("legend")));
            var dataDiv = legend.FindElement(By.ClassName("data"));
            string legendText = dataDiv.GetAttribute("innerHTML");

            // Pobieranie kursów z legendy
            var subjectPattern = new Regex(@"<strong>(.*?)</strong> - (.*?)(?:, występowanie|\s*<br|\s*<hr)", RegexOptions.Singleline);
            var subjects = subjectPattern.Matches(legendText).Cast<Match>().ToList();

            // Pobieranie div-ów zawierających informacje o kursach
            var courseDivs = driver.FindElements(By.XPath("//div[starts-with(@id, 'course_')]"));

            if (subjects.Any())
            {
                foreach (var match in subjects)
                {
                    string subjectCode = match.Groups[1].Value;
                    string subjectName = match.Groups[2].Value.Trim();

                    foreach (var div in courseDivs)
                    {
                        string divText = div.GetAttribute("innerHTML") ?? "";
                        if (divText.Contains(subjectCode))
                        {
                            string subjectType = GetSubjectType(divText);
                            string studyMode = GetStudyMode(subjectName + " " + divText);

                            if (studyMode != "nieznany")
                            {
                                var entry = new Dictionary<string, string>
                                {
                                    { "Katedra", deptName },
                                    { "Prowadzący", coordinatorName },
                                    { "Przedmiot", subjectName },
                                    { "Typ", subjectType },
                                    { "Tryb studiów", studyMode }
                                };

                                // Dodajemy wpis tylko jeśli takiego jeszcze nie ma
                                if (!EntryExists(entry))
                                {
                                    results.Add(entry);
                                    Console.WriteLine($"Zescrapowano plan: Katedra: {deptName}, Prowadzący: {coordinatorName}, Przedmiot: {subjectName}, Typ: {subjectType}, Tryb: {studyMode}");
                                }
                                else
                                {
                                    Console.WriteLine($"Pominięto duplikat: Katedra: {deptName}, Prowadzący: {coordinatorName}, Przedmiot: {subjectName}, Typ: {subjectType}, Tryb: {studyMode}");
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // Alternatywne podejście, gdy legenda nie zawiera oczekiwanych informacji
                foreach (var div in courseDivs)
                {
                    try
                    {
                        string divText = div.GetAttribute("innerHTML") ?? "";

                        // Próba wyciągnięcia nazwy przedmiotu z div-a
                        var contentMatch = Regex.Match(divText, @"<b>(.*?)</b>", RegexOptions.Singleline);
                        string subjectName = contentMatch.Success ?
                            contentMatch.Groups[1].Value.Trim() :
                            divText.Split(new[] { "<br>" }, StringSplitOptions.None)[0].Trim();

                        // Usunięcie ewentualnych tagów HTML z nazwy przedmiotu
                        subjectName = Regex.Replace(subjectName, "<.*?>", string.Empty);

                        if (!string.IsNullOrWhiteSpace(subjectName))
                        {
                            string subjectType = GetSubjectType(divText);
                            string studyMode = GetStudyMode(subjectName + " " + divText);

                            if (studyMode != "nieznany")
                            {
                                var entry = new Dictionary<string, string>
                                {
                                    { "Katedra", deptName },
                                    { "Prowadzący", coordinatorName },
                                    { "Przedmiot", subjectName },
                                    { "Typ", subjectType },
                                    { "Tryb studiów", studyMode }
                                };

                                // Dodajemy wpis tylko jeśli takiego jeszcze nie ma
                                if (!EntryExists(entry))
                                {
                                    results.Add(entry);
                                    Console.WriteLine($"Zescrapowano plan: Katedra: {deptName}, Prowadzący: {coordinatorName}, Przedmiot: {subjectName}, Typ: {subjectType}, Tryb: {studyMode}");
                                }
                                else
                                {
                                    Console.WriteLine($"Pominięto duplikat: Katedra: {deptName}, Prowadzący: {coordinatorName}, Przedmiot: {subjectName}, Typ: {subjectType}, Tryb: {studyMode}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Błąd przy przetwarzaniu pojedynczego kursu: {ex.Message}");
                    }
                }
            }

            // Dodatkowe sprawdzenie dla przypadków, gdy brak div course_
            if (!courseDivs.Any())
            {
                // Sprawdzanie całej tabeli z planem
                var scheduleTable = driver.FindElements(By.XPath("//table[@id='tab']")).FirstOrDefault();
                if (scheduleTable != null)
                {
                    var cells = scheduleTable.FindElements(By.XPath(".//td[contains(@id, 'td')]"));
                    foreach (var cell in cells)
                    {
                        try
                        {
                            string cellContent = cell.GetAttribute("innerHTML") ?? "";
                            if (!string.IsNullOrWhiteSpace(cellContent) && cellContent.Length > 10)
                            {
                                var courseMatch = Regex.Match(cellContent, @"<b>(.*?)</b>", RegexOptions.Singleline);
                                if (courseMatch.Success)
                                {
                                    string subjectName = courseMatch.Groups[1].Value.Trim();
                                    string subjectType = GetSubjectType(cellContent);
                                    string studyMode = GetStudyMode(cellContent);

                                    if (studyMode != "nieznany")
                                    {
                                        var entry = new Dictionary<string, string>
                                        {
                                            { "Katedra", deptName },
                                            { "Prowadzący", coordinatorName },
                                            { "Przedmiot", subjectName },
                                            { "Typ", subjectType },
                                            { "Tryb studiów", studyMode }
                                        };

                                        // Dodajemy wpis tylko jeśli takiego jeszcze nie ma
                                        if (!EntryExists(entry))
                                        {
                                            results.Add(entry);
                                            Console.WriteLine($"Zescrapowano z komórki: Katedra: {deptName}, Prowadzący: {coordinatorName}, Przedmiot: {subjectName}, Typ: {subjectType}, Tryb: {studyMode}");
                                        }
                                        else
                                        {
                                            Console.WriteLine($"Pominięto duplikat: Katedra: {deptName}, Prowadzący: {coordinatorName}, Przedmiot: {subjectName}, Typ: {subjectType}, Tryb: {studyMode}");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Błąd przy przetwarzaniu komórki: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Błąd w ScrapeSchedule dla {coordinatorName}: {e.Message}");
        }
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
    }//ssssssssssssss

    static Dictionary<string, Dictionary<string, Dictionary<string, object>>> TransformToJson(List<Dictionary<string, string>> results)
    {
        // Przygotowanie struktury dla danych JSON
        var jsonData = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>>>();

        // Grupowanie rekordów, aby zapobiec duplikatom
        var uniqueResults = results
            .GroupBy(r => new {
                Dept = r["Katedra"],
                Subject = r["Przedmiot"],
                Type = r["Typ"],
                Mode = r["Tryb studiów"],
                Coordinator = r["Prowadzący"]
            })
            .Select(g => g.First())
            .ToList();

        Console.WriteLine($"Po usunięciu duplikatów: {uniqueResults.Count} z {results.Count} rekordów");

        // Tworzenie hierarchicznej struktury JSON
        foreach (var entry in uniqueResults)
        {
            string dept = entry["Katedra"];
            string subject = entry["Przedmiot"];
            string subjectType = entry["Typ"];
            string studyMode = entry["Tryb studiów"];
            string coordinator = entry["Prowadzący"];

            if (!jsonData.ContainsKey(dept))
                jsonData[dept] = new Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>>();
            if (!jsonData[dept].ContainsKey(subject))
                jsonData[dept][subject] = new Dictionary<string, Dictionary<string, List<string>>>();
            if (!jsonData[dept][subject].ContainsKey(studyMode))
                jsonData[dept][subject][studyMode] = new Dictionary<string, List<string>>();
            if (!jsonData[dept][subject][studyMode].ContainsKey(subjectType))
                jsonData[dept][subject][studyMode][subjectType] = new List<string>();

            if (!jsonData[dept][subject][studyMode][subjectType].Contains(coordinator))
                jsonData[dept][subject][studyMode][subjectType].Add(coordinator);
        }

        // Konwersja do końcowej struktury JSON
        var finalJson = new Dictionary<string, Dictionary<string, Dictionary<string, object>>>();
        foreach (var dept in jsonData.Keys)
        {
            finalJson[dept] = new Dictionary<string, Dictionary<string, object>>();
            foreach (var subject in jsonData[dept].Keys)
            {
                finalJson[dept][subject] = new Dictionary<string, object>();
                foreach (var studyMode in jsonData[dept][subject].Keys)
                {
                    var types = jsonData[dept][subject][studyMode];
                    finalJson[dept][subject][studyMode] = types;
                }
            }
        }
        return finalJson;
    }
}