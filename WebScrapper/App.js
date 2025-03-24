import React, { useState, useEffect } from "react";
import "./App.css";

const App = () => {
  const [data, setData] = useState({});
  const [selectedSubject, setSelectedSubject] = useState("");
  const [selectedMode, setSelectedMode] = useState("");
  const [selectedCoordinator, setSelectedCoordinator] = useState("");
  const [selectedType, setSelectedType] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch("/PLAN.json")
      .then((res) => res.json())
      .then((jsonData) => {
        setData(jsonData);
        setLoading(false);
      })
      .catch((error) => {
        console.error("Błąd wczytywania danych:", error);
        setLoading(false);
      });
  }, []);

  // Funkcja do zebrania wszystkich unikalnych prowadzących, posortowanych alfabetycznie
  const getAllCoordinators = () => {
    const coordinators = new Set();
    Object.values(data).forEach((dept) => {
      Object.values(dept).forEach((subject) => {
        Object.values(subject).forEach((mode) => {
          mode.Prowadzący.forEach((coord) => coordinators.add(coord));
        });
      });
    });
    return Array.from(coordinators).sort((a, b) => a.localeCompare(b, "pl"));
  };

  // Funkcja do zebrania przedmiotów dla wybranego prowadzącego
  const getSubjectsForCoordinator = () => {
    const subjects = new Set();
    Object.values(data).forEach((dept) => {
      Object.entries(dept).forEach(([subject, modes]) => {
        Object.values(modes).forEach((mode) => {
          if (
            !selectedCoordinator ||
            mode.Prowadzący.includes(selectedCoordinator)
          ) {
            subjects.add(subject);
          }
        });
      });
    });
    return Array.from(subjects);
  };

  // Funkcja do zebrania trybów studiów dla wybranego przedmiotu i prowadzącego
  const getModesForSubjectAndCoordinator = (subject) => {
    if (!subject) return []; // Jeśli nie wybrano przedmiotu, zwracamy pustą listę
    const modes = new Set();
    Object.values(data).forEach((dept) => {
      if (dept[subject]) {
        Object.entries(dept[subject]).forEach(([mode, details]) => {
          if (
            !selectedCoordinator ||
            details.Prowadzący.includes(selectedCoordinator)
          ) {
            modes.add(mode);
          }
        });
      }
    });
    return Array.from(modes);
  };

  // Funkcja do zebrania wszystkich unikalnych typów zajęć dla wybranego prowadzącego i przedmiotu
  const getTypesForCoordinatorAndSubject = () => {
    const types = new Set();
    Object.values(data).forEach((dept) => {
      Object.entries(dept).forEach(([subject, modes]) => {
        if (!selectedSubject || subject === selectedSubject) {
          Object.values(modes).forEach((mode) => {
            if (
              (!selectedCoordinator ||
                mode.Prowadzący.includes(selectedCoordinator)) &&
              (!selectedMode || mode === selectedMode)
            ) {
              types.add(mode.Typ);
            }
          });
        }
      });
    });
    return Array.from(types);
  };

  // Funkcja do zebrania danych dla wybranego przedmiotu, trybu, prowadzącego i typu
  const getFilteredResults = () => {
    const results = [];
    Object.values(data).forEach((dept) => {
      Object.entries(dept).forEach(([subject, modes]) => {
        Object.entries(modes).forEach(([mode, details]) => {
          const matchesSubject = !selectedSubject || subject === selectedSubject;
          const matchesMode = !selectedMode || mode === selectedMode;
          const matchesCoordinator =
            !selectedCoordinator ||
            details.Prowadzący.includes(selectedCoordinator);
          const matchesType = !selectedType || details.Typ === selectedType;

          if (matchesSubject && matchesMode && matchesCoordinator && matchesType) {
            results.push({
              subject,
              mode,
              type: details.Typ,
              coordinators: details.Prowadzący,
            });
          }
        });
      });
    });
    return results;
  };

  if (loading) {
    return <div className="app-container">Ładowanie danych...</div>;
  }

  const coordinators = getAllCoordinators();
  const subjects = getSubjectsForCoordinator();
  const modes = getModesForSubjectAndCoordinator(selectedSubject);
  const types = getTypesForCoordinatorAndSubject();
  const filteredResults = getFilteredResults();

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
              setSelectedCoordinator(e.target.value);
              setSelectedSubject(""); // Reset przedmiotu
              setSelectedMode(""); // Reset trybu
              setSelectedType(""); // Reset typu
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
              setSelectedSubject(e.target.value);
              setSelectedMode(""); // Reset trybu
              setSelectedType(""); // Reset typu
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
              setSelectedMode(e.target.value);
              setSelectedType(""); // Reset typu
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
            onChange={(e) => setSelectedType(e.target.value)}
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
        (selectedSubject || selectedMode || selectedCoordinator || selectedType) ? (
          <div className="result-box">
            <h2 className="result-title">Wyniki</h2>
            {filteredResults.map((result, index) => (
              <div key={index} className="result-item">
                <p>
                  <span className="result-label">Przedmiot:</span> {result.subject}
                </p>
                <p>
                  <span className="result-label">Tryb studiów:</span> {result.mode}
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
        ) : selectedSubject || selectedMode || selectedCoordinator || selectedType ? (
          <div className="result-box">
            <p>Brak wyników dla wybranych kryteriów.</p>
          </div>
        ) : null}
      </div>
    </div>
  );
};

export default App;