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
    static IWebDriver? driver; // Sterownik przeglądarki Chrome
    static WebDriverWait? wait; // Obiekt oczekiwania na elementy strony
    static List<Dictionary<string, string>> results = new List<Dictionary<string, string>>(); // Lista przechowująca wyniki scrapowania
    static HashSet<string> processedDeptIds = new HashSet<string>(); // Zbiór przetworzonych ID katedr, aby uniknąć duplikatów

    // Słownik mapujący skróty typów zajęć na pełne nazwy
    static readonly Dictionary<string, string> typeMapping = new Dictionary<string, string>
    {
        { "lek", "lektorat" }, { "wyk", "wykład" }, { "ćw", "ćwiczenia" },
        { "proj", "projektowanie" }, { "lab", "laboratorium" }, { "wf", "ćwiczenia" },
        { "wr", "warsztaty" }, { "konw", "konwersatorium" }, { "sem", "seminarium" },
        { "pnj", "Praktyczna Nauka Języka" }
    };

    // Słownik mapujący skróty trybów studiów na pełne nazwy
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
            var options = new ChromeOptions(); // Opcje konfiguracji przeglądarki Chrome
            // options.AddArgument("--headless"); // Wyłączone dla debugowania - przeglądarka działa w tle
            options.AddArgument("--disable-gpu"); // Wyłącza akcelerację GPU
            options.AddArgument("--no-sandbox"); // Wyłącza sandbox dla stabilności
            driver = new ChromeDriver(options); // Inicjalizacja sterownika Chrome
            wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60)); // Ustawienie maksymalnego czasu oczekiwania na elementy

            Console.WriteLine("Nawigacja do strony...");
            driver.Navigate().GoToUrl("https://plany.ubb.edu.pl/left_menu.php?type=2"); // Przejście do strony z planami
            Thread.Sleep(2000); // Krótka przerwa na załadowanie strony
            Console.WriteLine("Strona załadowana, rozpoczynanie scrapowania...");

            // Lista wydziałów z ich ID, nazwami i powiązanymi katedrami
            var faculties = new List<(string facultyId, string facultyName, string branchParam, string[] deptIds)>
            {
                ("6179", "Jednostki Międzywydziałowe", "0", new[] { "6196", "6197" }),
                ("6168", "Wydział Budowy Maszyn i Informatyki", "0", new[] { "6180", "6174", "6175", "6176", "6181", "30847" }),
                ("6171", "Wydział Humanistyczno-Społeczny", "0", new[] { "6193", "76335", "6192", "150120" }),
                ("6170", "Wydział Inżynierii Materiałów, Budownictwa i Środowiska", "0", new[] { "150424" }),
                ("6178", "Wydział Nauk o Zdrowiu", "0", new[] { "76336", "76337", "76338", "76339" }),
                ("6169", "Wydział Zarządzania i Transportu", "0", new[] { "6184", "6185", "6188", "52698", "52699" })
            };

            // Przetwarzanie każdego wydziału
            foreach (var faculty in faculties)
            {
                Console.WriteLine($"Przetwarzanie wydziału: {faculty.facultyName} (ID: {faculty.facultyId})");
                ProcessFaculty(faculty.facultyId, faculty.facultyName, faculty.branchParam, faculty.deptIds);
            }

            Console.WriteLine($"Zebrano {results.Count} rekordów");
            if (results.Count > 0)
            {
                var jsonData = TransformToJson(results); // Transformacja wyników do formatu JSON
                // Zapis wyników do pliku JSON z pominięciem wartości null
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
            Console.WriteLine($"Błąd główny: {ex.Message}\n{ex.StackTrace}"); // Wyświetlanie szczegółów błędu
        }
        finally
        {
            Console.WriteLine("Zamykanie przeglądarki...");
            driver?.Quit(); // Zamykanie przeglądarki w bloku finally, aby zawsze się wykonało
        }
    }

    // Funkcja określająca typ zajęć na podstawie tekstu HTML
    static string GetSubjectType(string divText)
    {
        var match = Regex.Match(divText ?? "", @"<img[^>]*id=""arrow_course_\d+""[^>]*>(.*?)<br>", RegexOptions.Singleline);
        if (match.Success)
        {
            string text = match.Groups[1].Value.Trim().ToLower();
            // Zwraca pełną nazwę typu zajęć lub "nieznany", jeśli nie znaleziono
            return typeMapping.FirstOrDefault(kvp => text.Contains(kvp.Key)).Value ?? "nieznany";
        }
        return "nieznany";
    }

    // Funkcja określająca tryb studiów na podstawie tekstu
    static string GetStudyMode(string text)
    {
        foreach (var mode in studyModeMapping.Keys)
        {
            if (Regex.IsMatch(text, $@"\b{mode}\b"))
            {
                return studyModeMapping[mode]; // Zwraca pełną nazwę trybu studiów
            }
        }
        return "nieznany"; // Domyślna wartość, jeśli tryb nieznany
    }

    // Sprawdza, czy dany wpis już istnieje w wynikach
    static bool EntryExists(string dept, string coord, string subj, string subjType, string studyMode)
    {
        return results.Any(entry =>
            entry["Prowadzący"] == coord &&
            entry["Przedmiot"] == subj &&
            entry["Typ"] == subjType &&
            entry["Tryb studiów"] == studyMode);
    }

    // Przetwarzanie pojedynczej katedry
    static void ProcessDepartment(string deptId, string facultyName, string facultyId, string branchParam)
    {
        try
        {
            Console.WriteLine($"Rozpoczynanie przetwarzania katedry {deptId}");
            EnsureFacultyExpanded(facultyId, facultyName); // Upewnia się, że wydział jest rozwinięty

            IWebElement? plusik = null;
            try
            {
                // Oczekiwanie na ikonę "+" katedry i przewinięcie do niej
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
                processedDeptIds.Add(deptId); // Dodanie ID do przetworzonych, aby uniknąć ponownego przetwarzania
                return;
            }

            string deptName = $"Katedra {deptId}";

            // Rozwijanie katedry, jeśli jest zwinięta (ikona "plus.gif")
            if (plusik.GetAttribute("src")?.Contains("plus.gif") == true)
            {
                ExpandDepartment(plusik, deptId, branchParam, facultyId);
            }

            var divDept = wait!.Until(d => d.FindElement(By.Id($"div_{deptId}"))); // Oczekiwanie na div katedry
            var coordinatorLinks = divDept.FindElements(By.XPath(".//a[contains(@href, 'type=10')]")); // Wyszukiwanie linków do prowadzących
            if (!coordinatorLinks.Any())
            {
                coordinatorLinks = divDept.FindElements(By.TagName("a")); // Alternatywne wyszukiwanie linków
            }

            // Tworzenie listy unikalnych prowadzących z nazwami i URL-ami
            var coordinators = coordinatorLinks
                .Where(link => !string.IsNullOrWhiteSpace(link.Text))
                .Select(link => (Name: link.Text.Trim(), Url: link.GetAttribute("href")))
                .Distinct()
                .ToList();

            Console.WriteLine($"Znaleziono {coordinators.Count} prowadzących dla katedry {deptId}");
            foreach (var coordinator in coordinators)
            {
                ProcessCoordinator(coordinator, deptName, facultyId); // Przetwarzanie każdego prowadzącego
            }

            processedDeptIds.Add(deptId); // Oznaczenie katedry jako przetworzonej
        }
        catch (Exception e)
        {
            Console.WriteLine($"Błąd w ProcessDepartment (deptId: {deptId}): {e.Message}\n{e.StackTrace}");
            processedDeptIds.Add(deptId); // Dodanie do przetworzonych nawet w przypadku błędu
        }
    }

    // Funkcja zapewniająca rozwinięcie wydziału na stronie
    static void EnsureFacultyExpanded(string facultyId, string facultyName)
    {
        int attempts = 0;
        while (attempts < 3)
        {
            try
            {
                Console.WriteLine($"Próba rozwinięcia wydziału {facultyId}, próba {attempts + 1}");
                // Wykonanie skryptu JavaScript do rozwinięcia wydziału
                ((IJavaScriptExecutor)driver!).ExecuteScript($"branch(2,{facultyId},0,'{facultyName}');");
                wait!.Until(d => d.FindElement(By.Id(facultyId)).Displayed && d.FindElement(By.Id(facultyId)).Enabled);
                Thread.Sleep(2000); // Krótka przerwa na załadowanie
                Console.WriteLine($"Wydział {facultyId} rozwinięty");
                return;
            }
            catch (Exception ex)
            {
                attempts++;
                Console.WriteLine($"Błąd przy rozwijaniu wydziału {facultyId}: {ex.Message}");
                Thread.Sleep(2000); // Przerwa przed kolejną próbą
            }
        }
        throw new Exception($"Nie udało się rozwinąć wydziału {facultyId} po 3 próbach");
    }

    // Funkcja rozwijająca katedrę na stronie
    static void ExpandDepartment(IWebElement plusik, string deptId, string branchParam, string facultyId)
    {
        int attempts = 0;
        while (attempts < 3)
        {
            try
            {
                Console.WriteLine($"Próba rozwinięcia katedry {deptId}, próba {attempts + 1}");
                new Actions(driver!).MoveToElement(plusik).Click().Perform(); // Kliknięcie na ikonę "+"
                wait!.Until(d => d.FindElement(By.Id($"div_{deptId}")).Displayed); // Oczekiwanie na rozwinięcie
                Thread.Sleep(2000);
                Console.WriteLine($"Katedra {deptId} rozwinięta");
                return;
            }
            catch (Exception ex)
            {
                attempts++;
                Console.WriteLine($"Błąd przy rozwijaniu katedry {deptId}: {ex.Message}");
                // Alternatywne wywołanie skryptu JavaScript do rozwinięcia
                ((IJavaScriptExecutor)driver!).ExecuteScript($"get_left_tree_branch('{deptId}', 'img_{deptId}', 'div_{deptId}', '2', '{branchParam}');");
                Thread.Sleep(2000);
            }
        }
        throw new Exception($"Nie udało się rozwinąć katedry {deptId} po 3 próbach");
    }

    // Przetwarzanie danych pojedynczego prowadzącego
    static void ProcessCoordinator((string Name, string Url) coordinator, string deptName, string facultyId)
    {
        driver!.Navigate().GoToUrl(coordinator.Url); // Przejście do strony prowadzącego
        Thread.Sleep(2000);

        if (driver.FindElements(By.Id("legend")).Count == 0)
        {
            Console.WriteLine($"Brak legendy dla prowadzącego {coordinator.Name}");
            return; // Wyjście, jeśli brak danych
        }

        try
        {
            var legend = wait!.Until(d => d.FindElement(By.Id("legend"))); // Oczekiwanie na sekcję legendy
            var dataDiv = legend.FindElement(By.ClassName("data")); // Znajdowanie diva z danymi
            string legendText = dataDiv.GetAttribute("innerHTML"); // Pobieranie zawartości HTML

            // Wyrażenie regularne do wyodrębnienia kodów i nazw przedmiotów
            var subjectPattern = new Regex(@"<strong>(.*?)</strong> - (.*?)(?:, występowanie|\s*<br|\s*<hr)", RegexOptions.Singleline);
            var subjects = subjectPattern.Matches(legendText).Cast<Match>().ToList();
            var courseDivs = driver.FindElements(By.XPath("//div[starts-with(@id, 'course_')]")); // Wyszukiwanie divów z kursami

            if (subjects.Any())
            {
                // Przetwarzanie przedmiotów z wyraźnym kodem i nazwą
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
                            string studyMode = GetStudyMode(subjectName + " " + divText);
                            if (studyMode != "nieznany" && !EntryExists(deptName, coordinator.Name, subjectName, subjectType, studyMode))
                            {
                                // Tworzenie nowego wpisu do wyników
                                var entry = new Dictionary<string, string>
                                {
                                    { "Katedra", deptName },
                                    { "Prowadzący", coordinator.Name },
                                    { "Przedmiot", subjectName },
                                    { "Typ", subjectType },
                                    { "Tryb studiów", studyMode }
                                };
                                results.Add(entry);
                                Console.WriteLine($"Zescrapowano plan: Katedra: {deptName}, Prowadzący: {coordinator.Name}, Przedmiot: {subjectName}, Typ: {subjectType}, Tryb: {studyMode}");
                            }
                        }
                    }
                }
            }
            else
            {
                // Przetwarzanie przedmiotów bez wyraźnego kodu
                foreach (var div in courseDivs)
                {
                    string divText = div.GetAttribute("innerHTML") ?? "";
                    string subjectName = divText.Split(new[] { "<br>" }, StringSplitOptions.None)[0].Trim();
                    string subjectType = GetSubjectType(divText);
                    string studyMode = GetStudyMode(subjectName + " " + divText);
                    if (studyMode != "nieznany" && !EntryExists(deptName, coordinator.Name, subjectName, subjectType, studyMode))
                    {
                        var entry = new Dictionary<string, string>
                        {
                            { "Katedra", deptName },
                            { "Prowadzący", coordinator.Name },
                            { "Przedmiot", subjectName },
                            { "Typ", subjectType },
                            { "Tryb studiów", studyMode }
                        };
                        results.Add(entry);
                        Console.WriteLine($"Zescrapowano plan: Katedra: {deptName}, Prowadzący: {coordinator.Name}, Przedmiot: {subjectName}, Typ: {subjectType}, Tryb: {studyMode}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Błąd w ProcessCoordinator dla {coordinator.Name}: {e.Message}");
        }

        // Powrót do strony głównej po przetworzeniu prowadzącego
        driver.Navigate().GoToUrl("https://plany.ubb.edu.pl/left_menu.php?type=2");
        wait!.Until(d => d.FindElement(By.Id(facultyId)));
    }

    // Przetwarzanie wydziału i jego katedr
    static void ProcessFaculty(string facultyId, string facultyName, string branchParam, string[] deptIds)
    {
        try
        {
            driver!.Navigate().GoToUrl("https://plany.ubb.edu.pl/left_menu.php?type=2");
            EnsureFacultyExpanded(facultyId, facultyName); // Rozwinięcie wydziału

            foreach (var deptId in deptIds)
            {
                if (!processedDeptIds.Contains(deptId))
                {
                    ProcessDepartment(deptId, facultyName, facultyId, branchParam); // Przetwarzanie każdej katedry
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Błąd w ProcessFaculty: {e.Message}");
        }
    }

    // Transformacja wyników do hierarchicznego formatu JSON
    static Dictionary<string, Dictionary<string, Dictionary<string, object>>> TransformToJson(List<Dictionary<string, string>> results)
    {
        var jsonData = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>>>();
        foreach (var entry in results)
        {
            // Budowanie struktury: Katedra -> Przedmiot -> Tryb studiów -> Typ -> Lista prowadzących
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

        // Uproszczenie struktury JSON do ostatecznego formatu
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
                    if (types.ContainsKey("wykład"))
                    {
                        // Priorytet dla wykładów, jeśli istnieje
                        finalJson[dept][subject][studyMode] = new Dictionary<string, object>
                        {
                            { "Typ", "wykład" },
                            { "Prowadzący", types["wykład"] }
                        };
                    }
                    else
                    {
                        // W przeciwnym razie pierwszy znaleziony typ
                        var firstType = types.Keys.First();
                        finalJson[dept][subject][studyMode] = new Dictionary<string, object>
                        {
                            { "Typ", firstType },
                            { "Prowadzący", types[firstType] }
                        };
                    }
                }
            }
        }
        return finalJson;
    }
}