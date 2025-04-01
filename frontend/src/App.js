import React, { useState, useEffect } from "react";
import "./App.css";

// Główny komponent aplikacji
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

  // Funkcja zwracająca posortowaną listę wszystkich unikalnych prowadzących
  const getAllCoordinators = () => {
    const coordinators = new Set();
    Object.values(data).forEach((dept) => {
      Object.values(dept).forEach((subject) => {
        Object.values(subject).forEach((mode) => {
          Object.values(mode).forEach((typeDetails) => {
            if (Array.isArray(typeDetails)) {
              typeDetails.forEach((coord) => coordinators.add(coord));
            } else if (typeof typeDetails === "string") {
              coordinators.add(typeDetails);
            }
          });
        });
      });
    });
    return Array.from(coordinators).sort((a, b) => a.localeCompare(b, "pl"));
  };

  // Funkcja zwracająca listę przedmiotów dla wybranego prowadzącego
  const getSubjectsForCoordinator = () => {
    const subjects = new Set();
    Object.values(data).forEach((dept) => {
      Object.entries(dept).forEach(([subject, modes]) => {
        Object.values(modes).forEach((mode) => {
          Object.values(mode).forEach((typeDetails) => {
            if (
              !selectedCoordinator ||
              typeDetails.includes(selectedCoordinator)
            ) {
              subjects.add(subject);
            }
          });
        });
      });
    });
    return Array.from(subjects);
  };

  // Funkcja zwracająca tryby studiów dla wybranego przedmiotu i prowadzącego
  const getModesForSubjectAndCoordinator = (subject) => {
    if (!subject) return [];
    const modes = new Set();
    Object.values(data).forEach((dept) => {
      if (dept[subject]) {
        Object.keys(dept[subject]).forEach((mode) => {
          Object.values(dept[subject][mode]).forEach((typeDetails) => {
            if (
              !selectedCoordinator ||
              typeDetails.includes(selectedCoordinator)
            ) {
              modes.add(mode);
            }
          });
        });
      }
    });
    return Array.from(modes);
  };

  // Funkcja zwracająca typy zajęć dla wybranego przedmiotu, trybu i prowadzącego
  const getTypesForCoordinatorAndSubject = () => {
    const types = new Set();
    Object.values(data).forEach((dept) => {
      Object.entries(dept).forEach(([subject, modes]) => {
        if (!selectedSubject || subject === selectedSubject) {
          Object.entries(modes).forEach(([mode, typeDetails]) => {
            if (
              (!selectedCoordinator ||
                Object.values(typeDetails).some((coords) =>
                  coords.includes(selectedCoordinator)
                )) &&
              (!selectedMode || mode === selectedMode)
            ) {
              Object.keys(typeDetails).forEach((type) => types.add(type));
            }
          });
        }
      });
    });
    return Array.from(types);
  };

  // Funkcja zwracająca wyniki filtrowania
  const getFilteredResults = () => {
    const results = [];
    Object.values(data).forEach((dept) => {
      Object.entries(dept).forEach(([subject, modes]) => {
        Object.entries(modes).forEach(([mode, typeDetails]) => {
          Object.entries(typeDetails).forEach(([type, coordinators]) => {
            const matchesSubject =
              !selectedSubject || subject === selectedSubject;
            const matchesMode = !selectedMode || mode === selectedMode;
            const matchesCoordinator =
              !selectedCoordinator ||
              coordinators.includes(selectedCoordinator);
            const matchesType = !selectedType || type === selectedType;

            if (
              matchesSubject &&
              matchesMode &&
              matchesCoordinator &&
              matchesType
            ) {
              results.push({
                subject,
                mode,
                type,
                coordinators,
              });
            }
          });
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

        <div className="select-container">
          <label className="select-label">Wybierz prowadzącego</label>
          <select
            className="select-input"
            value={selectedCoordinator}
            onChange={(e) => {
              setSelectedCoordinator(e.target.value);
              setSelectedSubject("");
              setSelectedMode("");
              setSelectedType("");
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

        <div className="select-container">
          <label className="select-label">Wybierz przedmiot</label>
          <select
            className="select-input"
            value={selectedSubject}
            onChange={(e) => {
              setSelectedSubject(e.target.value);
              setSelectedMode("");
              setSelectedType("");
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

        <div className="select-container">
          <label className="select-label">Wybierz tryb studiów</label>
          <select
            className="select-input"
            value={selectedMode}
            onChange={(e) => {
              setSelectedMode(e.target.value);
              setSelectedType("");
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
