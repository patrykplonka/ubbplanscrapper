import React, { useState, useEffect } from "react";
import "./App.css";

// Główny komponent aplikacji
const App = () => {
  // Stan aplikacji: dane, wybrane filtry i status ładowania
  const [data, setData] = useState({});
  const [selectedSubject, setSelectedSubject] = useState("");
  const [selectedMode, setSelectedMode] = useState("");
  const [selectedCoordinator, setSelectedCoordinator] = useState("");
  const [selectedType, setSelectedType] = useState("");
  const [loading, setLoading] = useState(true);

  // Efekt pobierający dane z PLAN.json przy pierwszym renderowaniu
  useEffect(() => {
    fetch("/PLAN.json")
      .then((res) => res.json()) // Konwersja odpowiedzi na JSON
      .then((jsonData) => {
        setData(jsonData); // Ustawienie danych w stanie
        setLoading(false); // Wyłączenie ładowania po sukcesie
      })
      .catch((error) => {
        console.error("Błąd wczytywania danych:", error); // Logowanie błędu
        setLoading(false); // Wyłączenie ładowania w przypadku błędu
      });
  }, []); // Puste zależności - wykonuje się tylko raz

  // Reszta kodu pozostaje bez zmian...

  // Funkcja zwracająca posortowaną listę wszystkich unikalnych prowadzących
  const getAllCoordinators = () => {
    const coordinators = new Set();
    Object.values(data).forEach((dept) => {
      Object.values(dept).forEach((subject) => {
        Object.values(subject).forEach((mode) => {
          Object.values(mode).forEach((typeDetails) => {
            typeDetails.forEach((coord) => coordinators.add(coord));
          });
        });
      });
    });
    return Array.from(coordinators).sort((a, b) => a.localeCompare(b, "pl"));
  };

  // Funkcja zwracająca listę przedmiotów dla wybranego prowadzącego
  const getSubjectsForCoordinator = () => {
    const subjects = new Set(); // Zbiór unikalnych przedmiotów
    Object.values(data).forEach((dept) => {
      Object.entries(dept).forEach(([subject, modes]) => {
        Object.values(modes).forEach((mode) => {
          if (
            !selectedCoordinator || // Jeśli nie wybrano prowadzącego, dodaj wszystkie przedmioty
            mode.Prowadzący.includes(selectedCoordinator) // Albo jeśli prowadzący jest na liście
          ) {
            subjects.add(subject); // Dodanie przedmiotu do zbioru
          }
        });
      });
    });
    return Array.from(subjects); // Konwersja na tablicę
  };

  // Funkcja zwracająca tryby studiów dla wybranego przedmiotu i prowadzącego
  const getModesForSubjectAndCoordinator = (subject) => {
    if (!subject) return []; // Jeśli nie wybrano przedmiotu, zwracamy pustą tablicę
    const modes = new Set(); // Zbiór unikalnych trybów
    Object.values(data).forEach((dept) => {
      if (dept[subject]) {
        // Sprawdzamy, czy przedmiot istnieje w wydziale
        Object.entries(dept[subject]).forEach(([mode, details]) => {
          if (
            !selectedCoordinator || // Jeśli nie wybrano prowadzącego, dodaj wszystkie tryby
            details.Prowadzący.includes(selectedCoordinator) // Albo jeśli prowadzący jest na liście
          ) {
            modes.add(mode); // Dodanie trybu do zbioru
          }
        });
      }
    });
    return Array.from(modes); // Konwersja na tablicę
  };

  // Funkcja zwracająca typy zajęć dla wybranego przedmiotu, trybu i prowadzącego
  const getTypesForCoordinatorAndSubject = () => {
    const types = new Set(); // Zbiór unikalnych typów zajęć
    Object.values(data).forEach((dept) => {
      Object.entries(dept).forEach(([subject, modes]) => {
        if (!selectedSubject || subject === selectedSubject) {
          // Filtruj tylko wybrany przedmiot
          Object.entries(modes).forEach(([mode, details]) => {
            if (
              (!selectedCoordinator ||
                details.Prowadzący.includes(selectedCoordinator)) && // Filtr prowadzącego
              (!selectedMode || mode === selectedMode) // Filtr trybu
            ) {
              types.add(details.Typ); // Dodanie typu zajęć do zbioru
            }
          });
        }
      });
    });
    return Array.from(types); // Konwersja na tablicę
  };

  // Funkcja zwracająca wyniki filtrowania na podstawie wybranych kryteriów
  const getFilteredResults = () => {
    const results = []; // Tablica wyników
    Object.values(data).forEach((dept) => {
      Object.entries(dept).forEach(([subject, modes]) => {
        Object.entries(modes).forEach(([mode, details]) => {
          // Sprawdzanie zgodności z wybranymi filtrami
          const matchesSubject =
            !selectedSubject || subject === selectedSubject;
          const matchesMode = !selectedMode || mode === selectedMode;
          const matchesCoordinator =
            !selectedCoordinator ||
            details.Prowadzący.includes(selectedCoordinator);
          const matchesType = !selectedType || details.Typ === selectedType;

          if (
            matchesSubject &&
            matchesMode &&
            matchesCoordinator &&
            matchesType
          ) {
            results.push({
              subject, // Przedmiot
              mode, // Tryb
              type: details.Typ, // Typ zajęć
              coordinators: details.Prowadzący, // Lista prowadzących
            });
          }
        });
      });
    });
    return results; // Zwrócenie wyników
  };

  // Wyświetlanie komunikatu podczas ładowania danych
  if (loading) {
    return <div className="app-container">Ładowanie danych...</div>;
  }

  // Pobieranie danych do wyboru w selectach
  const coordinators = getAllCoordinators(); // Lista prowadzących
  const subjects = getSubjectsForCoordinator(); // Lista przedmiotów
  const modes = getModesForSubjectAndCoordinator(selectedSubject); // Lista trybów
  const types = getTypesForCoordinatorAndSubject(); // Lista typów zajęć
  const filteredResults = getFilteredResults(); // Wyniki filtrowania

  // Logowanie do debugowania (opcjonalne)
  console.log("Selected Subject:", selectedSubject);
  console.log("Selected Mode:", selectedMode);
  console.log("Available Types:", types);

  // Renderowanie interfejsu użytkownika
  return (
    <div className="app-container">
      <div className="schedule-box">
        <h1 className="title">Plan zajęć</h1>

        {/* Wybór prowadzącego */}
        <div className="select-container">
          <label className="select-label">Wybierz prowadzącego</label>
          <select
            className="select-input"
            value={selectedCoordinator}
            onChange={(e) => {
              setSelectedCoordinator(e.target.value); // Ustawienie wybranego prowadzącego
              setSelectedSubject(""); // Reset wyboru przedmiotu
              setSelectedMode(""); // Reset wyboru trybu
              setSelectedType(""); // Reset wyboru typu
            }}
          >
            <option value="">-- Wybierz prowadzącego --</option>
            {coordinators.map((coord) => (
              <option key={coord} value={coord}>
                {coord}
              </option>
            ))}
          </select>
        </div>

        {/* Wybór przedmiotu */}
        <div className="select-container">
          <label className="select-label">Wybierz przedmiot</label>
          <select
            className="select-input"
            value={selectedSubject}
            onChange={(e) => {
              setSelectedSubject(e.target.value); // Ustawienie wybranego przedmiotu
              setSelectedMode(""); // Reset wyboru trybu
              setSelectedType(""); // Reset wyboru typu
            }}
          >
            <option value="">-- Wybierz przedmiot --</option>
            {subjects.map((subject) => (
              <option key={subject} value={subject}>
                {subject}
              </option>
            ))}
          </select>
        </div>

        {/* Wybór trybu studiów */}
        <div className="select-container">
          <label className="select-label">Wybierz tryb studiów</label>
          <select
            className="select-input"
            value={selectedMode}
            onChange={(e) => {
              setSelectedMode(e.target.value); // Ustawienie wybranego trybu
              setSelectedType(""); // Reset wyboru typu
            }}
          >
            <option value="">-- Wybierz tryb --</option>
            {modes.map((mode) => (
              <option key={mode} value={mode}>
                {mode}
              </option>
            ))}
          </select>
        </div>

        {/* Wybór typu zajęć */}
        <div className="select-container">
          <label className="select-label">Wybierz typ zajęć</label>
          <select
            className="select-input"
            value={selectedType}
            onChange={(e) => setSelectedType(e.target.value)} // Ustawienie wybranego typu
          >
            <option value="">-- Wybierz typ --</option>
            {types.map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </select>
        </div>

        {/* Wyświetlanie wyników */}
        {filteredResults.length > 0 &&
        (selectedSubject ||
          selectedMode ||
          selectedCoordinator ||
          selectedType) ? (
          <div className="result-box">
            <h2 className="result-title">Wyniki</h2>
            {filteredResults.map((result, index) => (
              <div key={index} className="result-item">
                <p>
                  <span className="result-label">Przedmiot:</span>{" "}
                  {result.subject}
                </p>
                <p>
                  <span className="result-label">Tryb studiów:</span>{" "}
                  {result.mode}
                </p>
                <p>
                  <span className="result-label">Typ zajęć:</span> {result.type}
                </p>
                <p>
                  <span className="result-label">Prowadzący:</span>{" "}
                  {result.coordinators.join(", ")}
                </p>
              </div>
            ))}
          </div>
        ) : selectedSubject ||
          selectedMode ||
          selectedCoordinator ||
          selectedType ? (
          <div className="result-box">
            <p>Brak wyników dla wybranych kryteriów.</p>
          </div>
        ) : null}
      </div>
    </div>
  );
};

export default App;
