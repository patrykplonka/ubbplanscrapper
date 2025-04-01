import React, { useState, useEffect, useMemo } from "react";
import "./App.css";

const App = () => {
  const [data, setData] = useState({}); // Przechowuje dane z pliku PLAN.json
  const [selectedSubject, setSelectedSubject] = useState(""); // Wybrany przedmiot
  const [selectedMode, setSelectedMode] = useState(""); // Wybrany tryb studiów
  const [selectedCoordinator, setSelectedCoordinator] = useState(""); // Wybrany prowadzący
  const [selectedType, setSelectedType] = useState(""); // Wybrany typ zajęć
  const [loading, setLoading] = useState(true); // Status ładowania danych
  const [error, setError] = useState(null); // Stan błędu

  useEffect(() => {
    fetch("/PLAN.json")
      .then((res) => {
        if (!res.ok) {
          throw new Error(`HTTP error! status: ${res.status}`);
        }
        return res.json();
      })
      .then((jsonData) => {
        setData(jsonData);
        setLoading(false);
      })
      .catch((error) => {
        console.error("Błąd wczytywania danych:", error);
        setError(error.message);
        setLoading(false);
      });
  }, []);

  const resetFilters = () => {
    setSelectedCoordinator("");
    setSelectedSubject("");
    setSelectedMode("");
    setSelectedType("");
  };

  // Funkcja zwracająca posortowaną listę wszystkich unikalnych prowadzących
  const getAllCoordinators = () => {
    if (!data || Object.keys(data).length === 0) return [];

    const coordinators = new Set();
    Object.values(data).forEach((dept) => {
      Object.values(dept).forEach((subject) => {
        Object.values(subject).forEach((mode) => {
          if (mode.Prowadzący && Array.isArray(mode.Prowadzący)) {
            mode.Prowadzący.forEach((coord) => coordinators.add(coord));
          }
        });
      });
    });
    return Array.from(coordinators).sort((a, b) => a.localeCompare(b, "pl"));
  };

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const getSubjectsForCoordinator = () => {
    if (!data || Object.keys(data).length === 0) return [];

    const subjects = new Set();
    Object.values(data).forEach((dept) => {
      Object.entries(dept).forEach(([subject, modes]) => {
        Object.values(modes).forEach((mode) => {
          if (mode.Prowadzący && Array.isArray(mode.Prowadzący)) {
            if (
              !selectedCoordinator ||
              mode.Prowadzący.includes(selectedCoordinator)
            ) {
              subjects.add(subject);
            }
          }
        });
      });
    });
    return Array.from(subjects).sort((a, b) => a.localeCompare(b, "pl"));
  };

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const getModesForSubjectAndCoordinator = (subject) => {
    if (!data || Object.keys(data).length === 0 || !subject) return [];

    const modes = new Set();
    Object.values(data).forEach((dept) => {
      if (dept[subject]) {
        Object.entries(dept[subject]).forEach(([mode, details]) => {
          if (details.Prowadzący && Array.isArray(details.Prowadzący)) {
            if (
              !selectedCoordinator ||
              details.Prowadzący.includes(selectedCoordinator)
            ) {
              modes.add(mode);
            }
          }
        });
      }
    });
    return Array.from(modes).sort();
  };

  const getTypesForCoordinatorAndSubject = () => {
    if (!data || Object.keys(data).length === 0) return [];

    const types = new Set();
    Object.values(data).forEach((dept) => {
      Object.entries(dept).forEach(([subject, modes]) => {
        if (!selectedSubject || subject === selectedSubject) {
          Object.entries(modes).forEach(([mode, details]) => {
            if (
              details.Prowadzący &&
              Array.isArray(details.Prowadzący) &&
              details.Typ
            ) {
              if (
                (!selectedCoordinator ||
                  details.Prowadzący.includes(selectedCoordinator)) &&
                (!selectedMode || mode === selectedMode)
              ) {
                types.add(details.Typ);
              }
            }
          });
        }
      });
    });
    return Array.from(types).sort();
  };

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const getFilteredResults = () => {
    if (!data || Object.keys(data).length === 0) return [];

    const results = [];
    Object.values(data).forEach((dept) => {
      Object.entries(dept).forEach(([subject, modes]) => {
        Object.entries(modes).forEach(([mode, details]) => {
          if (
            details.Prowadzący &&
            Array.isArray(details.Prowadzący) &&
            details.Typ
          ) {
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
                subject,
                mode,
                type: details.Typ,
                coordinators: details.Prowadzący,
              });
            }
          }
        });
      });
    });
    return results;
  };

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const coordinators = useMemo(() => getAllCoordinators(), [data]);
  const subjects = useMemo(
    () => getSubjectsForCoordinator(),
    [getSubjectsForCoordinator]
  );
  const modes = useMemo(
    () => getModesForSubjectAndCoordinator(selectedSubject),
    [getModesForSubjectAndCoordinator, selectedSubject]
  );
  const types = useMemo(
    () => getTypesForCoordinatorAndSubject(),
    [getTypesForCoordinatorAndSubject]
  );
  const filteredResults = useMemo(
    () => getFilteredResults(),
    [getFilteredResults]
  );

  // const hasAnyFilter =
  //   selectedSubject || selectedMode || selectedCoordinator || selectedType;

  // if (loading) {
  //   return <div className="app-container">Ładowanie danych...</div>;
  // }

  // if (error) {
  //   return <div className="app-container">Błąd ładowania danych: {error}</div>;
  // }

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

        {/* Wybór przedmiotu */}
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
            disabled={subjects.length === 0}
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
              setSelectedType("");
            }}
            disabled={modes.length === 0}
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
            disabled={types.length === 0}
          >
            <option value="">-- Wybierz typ --</option>
            {types.map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </select>
        </div>

        {/* Przycisk do resetowania filtrów */}
        <div className="select-container">
          <button
            className="reset-button"
            onClick={resetFilters}
            disabled={!hasAnyFilter}
          >
            Resetuj filtry
          </button>
        </div>

        {/* Wyświetlanie wyników */}
        {hasAnyFilter && (
          <div className="result-box">
            <h2 className="result-title">Wyniki</h2>
            {filteredResults.length > 0 ? (
              filteredResults.map((result, index) => (
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
                    <span className="result-label">Typ zajęć:</span>{" "}
                    {result.type}
                  </p>
                  <p>
                    <span className="result-label">Prowadzący:</span>{" "}
                    {result.coordinators.join(", ")}
                  </p>
                </div>
              ))
            ) : (
              <p>Brak wyników dla wybranych kryteriów.</p>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

export default App;
