# Redisseny Ciclorama: pla d'implementació

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convertir LayerLog en una app organitzada per figures, amb les vistes Col·lecció i Figura i la identitat visual Ciclorama, sense perdre cap funció ni dada.

**Architecture:** La lògica nova va a `LayerLog.Core`, que és l'única part amb proves: metadades de figura, catàleg, vocabulari i comparació. `LayerLog.App` passa de dues pestanyes a dues pantalles amb navegació, reutilitzant `ViewerView` per a l'escena i `HistoryViewModel` per a les dades d'un intent.

**Tech Stack:** .NET 10, WPF, CommunityToolkit.Mvvm, HelixToolkit.Wpf.SharpDX, SQLite, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-01-redisseny-ciclorama-design.md`

## Global Constraints

- Cap dada existent es perd: gcode, impressions, notes, fotos i enllaços.
- `Totoro2.stl` (1,05 milions de triangles) ha de continuar girant amb fluïdesa.
- Colors: panell `#142731`, tinta `#EFEBE2`, groc `#F2B544` només per a canvis i valoració, botó principal ivori.
- Tipografia: Hanken Grotesk inclosa a l'app, xifres tabulars, títol de figura a 34 px.
- Text normal amb contrast mínim 4,5:1. Valoració mai només amb color.
- Text d'interfície en català; codi i comentaris en anglès, amb la densitat de comentaris del codi actual.
- Vocabulari de la spec: figura, peça, intent, impressió. «Sense imprimir», mai «No imprès».
- WPF no suporta eixos de fonts variables: cal incloure fitxers estàtics (Regular, Medium, SemiBold, Bold).
- Mai s'escriu a la SD, a la carpeta de models ni als fitxers de Creality Print.

**Com es prova la UI:** aquest repositori no té proves d'App. Per a cada tasca d'UI, copia `%LOCALAPPDATA%\LayerLog` a una carpeta temporal, executa l'app amb `LAYERLOG_DATA` apuntant-hi, captura la finestra i comprova el que digui la tasca. Mai es prova contra les dades reals. `LAYERLOG_DATA` també talla l'SD: per provar la importació cal un test manual a part, a la tasca 11.

**Git:** cada fase és una branca des de `main` al dia i una PR amb squash. El primer commit de la fase 1 inclou la spec i aquest pla.

## Review Focus

- **Model reanomenat o mogut amb nom propi:** el nom i la marca de peça el segueixen. Prova a la tasca 1.
- **Model editat al mateix camí (hash nou):** conserva el nom. Prova a la tasca 1.
- **Model esborrat amb gcode enllaçats:** els seus gcode apareixen a «Sense figura», no desapareixen. Prova a la tasca 2.
- **Gcode amb dos objectes (`2Tyrinids`):** compta com a intent a les dues figures, i la seva valoració compta a totes dues. Prova a la tasca 2.
- **Nom buit o només espais:** torna al nom del fitxer. Prova a la tasca 1.

---

# Fase 1: vocabulari i dades

Branca: `feat/figures-core`.

### Task 1: Metadades de figura

**Files:**
- Modify: `src/LayerLog.Core/Data/Database.cs`, `src/LayerLog.Core/Data/Records.cs`, `src/LayerLog.Core/Data/HistoryRepository.cs`
- Test: `tests/LayerLog.Core.Tests/FigureMetaTests.cs`

**Interfaces:**
- Produces: `record FigureMeta(string RelativePath, string? Alias, bool IsPart)`
- Produces: `HistoryRepository.FigureMetas() -> IReadOnlyDictionary<string, FigureMeta>`
- Produces: `HistoryRepository.SetAlias(string relativePath, string hash, long size, string? alias)`
- Produces: `HistoryRepository.SetIsPart(string relativePath, string hash, long size, bool isPart)`

- [ ] **Step 1: Escriu les proves que fallen**

Fes servir el patró de `HistoryRepositoryTests` (base de dades temporal). Proves:

```csharp
[Fact] public void Alias_is_saved_and_blank_clears_it()
// SetAlias(p,"h",1,"Totoro") -> Alias == "Totoro"; SetAlias(p,"h",1,"   ") -> Alias null

[Fact] public void Alias_and_part_flag_are_independent()
// SetIsPart(true) després de SetAlias conserva l'àlies, i a l'inrevés

[Fact] public void Moved_model_keeps_its_alias()
// fitxer real a dir/a.stl amb àlies; mou-lo a dir/b/a.stl; RelinkMovedModels; la clau és b\a.stl amb el mateix àlies

[Fact] public void Edited_model_at_same_path_keeps_its_alias()
// canvia el contingut; RelinkMovedModels; l'àlies continua

[Fact] public void Existing_version_2_database_migrates_without_losing_prints()
// crea una base amb PRAGMA user_version=2 i una impressió; obre HistoryRepository; la impressió hi és i FigureMetas és buit
```

- [ ] **Step 2: Executa i comprova que fallen**

Run: `dotnet test --filter FigureMetaTests`
Expected: FAIL, no compila perquè `FigureMetas` no existeix.

- [ ] **Step 3: Implementa-ho**

Migració `version < 3` a `Database.Migrate`, amb `PRAGMA user_version = 3`:

```sql
CREATE TABLE figure_meta (
    relative_path TEXT PRIMARY KEY,
    model_hash TEXT NOT NULL,
    model_size INTEGER NOT NULL,
    alias TEXT,
    is_part INTEGER NOT NULL DEFAULT 0
);
```

`SetAlias` i `SetIsPart` fan upsert i retallen espais. Amplia `RelinkMovedModels` perquè mogui també `figure_meta`, amb el mateix criteri de mida i hash.

- [ ] **Step 4: Executa totes les proves**

Run: `dotnet test`
Expected: PASS, cap error.

- [ ] **Step 5: Commit**

```bash
git add docs src tests
git commit -m "feat: guarda el nom i el tipus de cada figura"
```

### Task 2: Catàleg de figures

**Files:**
- Create: `src/LayerLog.Core/Models/FigureCatalog.cs`
- Modify: `src/LayerLog.Core/Data/HistoryRepository.cs`, `src/LayerLog.Core/Models/ModelSearch.cs`
- Test: `tests/LayerLog.Core.Tests/FigureCatalogTests.cs`

**Interfaces:**
- Consumes: `FigureMeta`, `ModelFile`
- Produces: `record FigureStats(int Attempts, DateTime? LastAttempt, int? BestRating, string? BestProfile)`
- Produces: `HistoryRepository.FigureStats() -> IReadOnlyDictionary<string, FigureStats>`
- Produces: `HistoryRepository.EntriesWithoutFigure(IReadOnlySet<string> existingModels) -> IReadOnlyList<GcodeEntry>`
- Produces: `record Figure(string RelativePath, string Name, string Folder, bool IsPart, FigureStats Stats)`
- Produces: `FigureCatalog.Build(IEnumerable<ModelFile>, IReadOnlyDictionary<string, FigureMeta>, IReadOnlyDictionary<string, FigureStats>) -> IReadOnlyList<Figure>`
- Produces: `FigureCatalog.Recent(IEnumerable<Figure>, int count) -> IReadOnlyList<Figure>`
- Changes: `ModelSearch.Matches(Figure figure, string query, FigureFilter filter) -> bool`

- [ ] **Step 1: Escriu les proves que fallen**

```csharp
[Fact] public void Name_uses_alias_then_file_name()      // "Totoro" i després "3" per a 3.stl
[Fact] public void Gcode_with_two_objects_counts_for_both_figures()
// un gcode enllaçat a 3.stl i 5.stl: Attempts==1 a totes dues; BestRating igual a totes dues
[Fact] public void Best_rating_breaks_ties_with_the_most_recent_print()
[Fact] public void Unrated_figure_has_no_best_rating()
[Fact] public void Gcode_of_deleted_model_is_without_figure()
// enllaçat a x.stl, existingModels no el conté -> surt a EntriesWithoutFigure
[Fact] public void Gcode_linked_to_one_existing_figure_is_not_without_figure()
// dos objectes, un existeix i l'altre no -> no surt
[Fact] public void Recent_ignores_figures_without_attempts_and_parts()
[Fact] public void Search_finds_the_alias_and_folder_ignoring_accents()
```

- [ ] **Step 2: Executa i comprova que fallen**

Run: `dotnet test --filter FigureCatalogTests`
Expected: FAIL, no compila.

- [ ] **Step 3: Implementa-ho**

`Folder` és el directori relatiu, o buit a l'arrel. `FigureStats()` fa una sola consulta SQL agrupada, sense consultes per fila. `BestProfile` és el `process_profile` crua; l'App l'escurça amb `Profiles.Short`. Actualitza les crides antigues a `ModelSearch.Matches` i les proves de `ModelBrowsingTests`, mantenint-ne els casos.

- [ ] **Step 4: Executa totes les proves**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: afegeix el catàleg de figures"
```

### Task 3: Vocabulari i canvis clau

**Files:**
- Create: `src/LayerLog.Core/Models/AttemptText.cs`, `src/LayerLog.Core/Profiles/KeyChanges.cs`
- Modify: `src/LayerLog.App/History/HistoryViewModel.cs`, `src/LayerLog.App/Viewer/ViewerViewModel.cs`
- Test: `tests/LayerLog.Core.Tests/AttemptTextTests.cs`

**Interfaces:**
- Produces: `AttemptText.State(int prints) -> string`
- Produces: `KeyChanges.Pick(IEnumerable<ConfigChange> changes, int count) -> IReadOnlyList<ConfigChange>`

- [ ] **Step 1: Escriu les proves que fallen**

```csharp
[Theory]
[InlineData(0, "Sense imprimir")]
[InlineData(1, "1 impressió")]
[InlineData(3, "3 impressions")]
public void State_uses_the_agreed_words(int prints, string text) => Assert.Equal(text, AttemptText.State(prints));

[Fact] public void Pick_orders_by_importance_then_known_labels()
// ordre: layer_height, wall_loops, outer_wall_speed, raft_layers, support_type, nozzle_temperature, hot_plate_temp; després els coneguts; els desconeguts al final
[Fact] public void Pick_returns_all_when_there_are_fewer_than_count()
```

- [ ] **Step 2: Executa i comprova que fallen**

Run: `dotnet test --filter AttemptTextTests`
Expected: FAIL, no compila.

- [ ] **Step 3: Implementa-ho i substitueix les dues frases duplicades** de `HistoryViewModel.Refresh` i `ViewerViewModel.UpdateFigure` per `AttemptText.State`. Canvia el títol «Impressions» del panell de `ViewerView.xaml` per «Intents».

- [ ] **Step 4: Executa les proves**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit i PR**

```bash
git add src tests
git commit -m "feat: unifica el vocabulari d'intents i impressions"
git push -u origin feat/figures-core
gh pr create --fill
```

---

# Fase 2: base visual

Branca: `feat/base-ciclorama`.

### Task 4: Tokens, tipografia i focus

**Files:**
- Create: `src/LayerLog.App/Fonts/` amb `HankenGrotesk-{Regular,Medium,SemiBold,Bold}.ttf` i `OFL.txt`
- Modify: `src/LayerLog.App/Theme.xaml`, `src/LayerLog.App/LayerLog.App.csproj`, `src/LayerLog.App/History/HistoryView.xaml`, `src/LayerLog.App/Viewer/ViewerView.xaml`

**Interfaces:**
- Produces: pinzells `Sweep` `#233D47`, `Panel` `#142731`, `Raised` `#2A4048`, `Ink` `#EFEBE2`, `Muted` `#A9BABD`, `Change` `#F2B544`, `Action` `#EFEBE2`, `ActionText` `#142731`
- Produces: estils `Title` (34 px, Bold), `H2` (15 px, SemiBold), `FocusRing`

- [ ] **Step 1: Baixa les fonts estàtiques i `OFL.txt`** de Google Fonts, amb permís de l'usuari, i marca-les com a `Resource`.

- [ ] **Step 2: Substitueix els tokens.** Elimina `Primary`, `PrimaryHover`, `PrimaryText`, `Selected`, `Ivory` i `SceneShadow`, sense deixar cap `StaticResource` penjat. Els enllaços passen a tinta subratllada. `PrimaryButton` usa `Action` i `ActionText`.

- [ ] **Step 3: Afegeix focus visible.** Un `FocusVisualStyle` global de 2 px en tinta a `FlatButton`, `LinkButton`, `RowButton`, `TabButton`, `TextBox` i `CheckBox`, i estat de focus al template de cada un.

- [ ] **Step 4: Comprova-ho**

Run: `dotnet build src/LayerLog.App`
Expected: 0 errors. Executa l'app, recorre-la amb Tab i confirma a la captura que el focus es veu a tots els controls. Confirma que no queda cap rosa amb `rg "CF1E64|FF859F|Primary\b" src/LayerLog.App`.

- [ ] **Step 5: Commit**

```bash
git add src
git commit -m "feat: aplica els colors i la tipografia de Ciclorama"
```

### Task 5: Barra de títol, escena i DESIGN.md

**Files:**
- Modify: `src/LayerLog.App/MainWindow.xaml`, `src/LayerLog.App/MainWindow.xaml.cs`, `src/LayerLog.App/Viewer/ViewerView.xaml`, `src/LayerLog.App/Viewer/ViewerView.xaml.cs`, `src/LayerLog.Core/Models/ModelThumbnail.cs`, `src/LayerLog.App/Viewer/ThumbnailStore.cs`
- Create: `DESIGN.md`

- [ ] **Step 1: Barra de títol pròpia** amb `WindowChrome` (`CaptionHeight=52`, `ResizeBorderThickness=6`). Botons de minimitzar, maximitzar i tancar accessibles, amb `AutomationProperties.Name` en català i `WindowChrome.IsHitTestVisibleInChrome="True"` a tot el que es pugui clicar. En maximitzar, compensa el marge perquè no es talli.

- [ ] **Step 2: Escena.** Substitueix `BuildFloor` per `BuildSweep(float span)`: terra, corba i paret amb colors per vèrtex entre `#4C717D` al centre i `#233D47` a les vores, més una ombra de contacte sota la figura. Treu els dos `Rectangle` de degradat. Si `Frame` deixa la figura sota el 60 % de l'alçada, redueix `orbitRadius`.

- [ ] **Step 3: Comprova bandes i fluïdesa.** Captura amb `Totoro2.stl`. Si es veuen bandes o baixa la fluïdesa, aplica l'alternativa de la spec: `BackgroundColor` transparent i una `Image` pre-renderitzada amb soroll darrere el `Viewport3DX`.

- [ ] **Step 4: Miniatures.** Puja `ModelThumbnail.Size` a 320 i afegeix-lo al text de hash de `ThumbnailStore.PathFor`, perquè es regenerin. Material ivori, el mateix que `ViewerView`.

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Escriu `DESIGN.md`** a partir de l'app construïda: colors, tipus, espais, regla d'acció i regla de canvis.

- [ ] **Step 6: Commit i PR**

```bash
git add src DESIGN.md
git commit -m "feat: afegeix la barra de títol i l'escena Ciclorama"
git push -u origin feat/base-ciclorama
gh pr create --fill
```

---

# Fase 3: Col·lecció

Branca: `feat/colleccio`.

### Task 6: Pantalla Col·lecció

**Files:**
- Create: `src/LayerLog.App/Collection/CollectionView.xaml`, `CollectionView.xaml.cs`, `CollectionViewModel.cs`, `FigureCard.cs`
- Modify: `src/LayerLog.App/MainViewModel.cs`, `src/LayerLog.App/MainWindow.xaml`, `src/LayerLog.App/Viewer/ViewerViewModel.cs`, `src/LayerLog.App/Viewer/ViewerView.xaml`, `src/LayerLog.App/Viewer/ViewerView.xaml.cs`
- Delete: `src/LayerLog.App/Viewer/ExplorerNode.cs`

**Interfaces:**
- Consumes: `FigureCatalog.Build`, `FigureCatalog.Recent`, `ModelSearch.Matches`, `HistoryRepository.EntriesWithoutFigure`
- Produces: `enum Screen { Collection, Figure }`, `MainViewModel.Screen`, `MainViewModel.OpenFigure(string relativePath)`, `MainViewModel.BackCommand`
- Produces: `CollectionViewModel.Sections`, `Recent`, `WithoutFigure`, `Query`, `SendCommand`, `ChangeFolderCommand`
- Produces: `FigureCard : ObservableObject` amb `Figure`, `Thumbnail`, `IsMarked`, `IsSelected`

- [ ] **Step 1: Mou l'escaneig** de `ViewerViewModel` a `CollectionViewModel`: carpeta, `ModelWatcher`, enllaços, miniatures i enviament a Creality Print. `ViewerViewModel` queda amb la càrrega del model i les dades de la figura.

- [ ] **Step 2: Construeix la vista** segons l'artboard «A · Ciclorama: col·lecció»: barra amb cerca i filtres, «Recents» i seccions per carpeta plegables. Usa `VirtualizingWrapPanel` propi només si cal; primer un `ListBox` amb `ItemsPanel` en files virtualitzades per secció. Marca la decisió amb un comentari `ponytail:` si queda una limitació de mida.

- [ ] **Step 3: Accions de figura.** Menú contextual i tecla F2 a cada targeta: «Canviar el nom» i «Marcar com a peça». Una opció «Mostrar les peces» a la secció. La selecció múltiple manté `CrealityLauncher.GroupWarningNeeded` i el seu avís.

- [ ] **Step 4: Inici.** `MainViewModel.StartAsync` obre Figura si `LastModel` existeix i Col·lecció si no.

- [ ] **Step 5: Comprova-ho** amb una còpia de les dades: 72 fitxers, cerca «tyranid», filtre Impreses, canviar nom, marcar peça i tornar, enviar una figura i enviar-ne dues a Creality Print, carpeta buida i cap resultat.

- [ ] **Step 6: Commit i PR**

```bash
git add src
git commit -m "feat: afegeix la pantalla Col·lecció"
git push -u origin feat/colleccio
gh pr create --fill
```

---

# Fase 4: Figura

Branca: `feat/pantalla-figura`.

### Task 7: Panell de figura

**Files:**
- Create: `src/LayerLog.App/Viewer/FigurePanel.xaml`, `FigurePanel.xaml.cs`
- Modify: `src/LayerLog.App/Viewer/ViewerView.xaml`, `src/LayerLog.App/Viewer/ViewerViewModel.cs`

**Interfaces:**
- Consumes: `KeyChanges.Pick`, `AttemptText.State`, `HistoryRepository.ListForModel`, `HistoryRepository.BestPrint`
- Produces: `ViewerViewModel.BestNote`, `BestChanges`, `Attempts`, `SelectedAttempt`

- [ ] **Step 1: Panell opac de 390 px** a la dreta, amb nom, carpeta i fitxer, «El que va funcionar» i «Altres intents». `Viewport3DX` ocupa la resta, i `Frame` centra la figura en aquesta zona.

- [ ] **Step 2: Estats.** Figura sense intents, intents sense valorar, cap nota, comparació aproximada i perfil base que falta. Quan no hi ha valoració, el títol és «Intents» i no es mostra cap «millor».

- [ ] **Step 3: Comprova-ho** amb Totoro, amb `3` (dos gcode) i amb una figura sense intents. Compta un sol clic des de Col·lecció fins a la millor configuració.

- [ ] **Step 4: Commit**

```bash
git add src
git commit -m "feat: afegeix el panell de figura"
```

### Task 8: Intents al panell i fi de l'Historial

**Files:**
- Create: `src/LayerLog.App/Viewer/AttemptPanel.xaml`, `AttemptPanel.xaml.cs`
- Move: `src/LayerLog.App/History/PrintRow.cs` a `src/LayerLog.App/Viewer/PrintRow.cs`
- Rename: `HistoryViewModel` a `AttemptViewModel`, a `src/LayerLog.App/Viewer/AttemptViewModel.cs`
- Delete: `src/LayerLog.App/History/HistoryView.xaml`, `HistoryView.xaml.cs`
- Modify: `src/LayerLog.App/MainViewModel.cs`, `src/LayerLog.App/MainWindow.xaml`

**Interfaces:**
- Produces: `AttemptViewModel.Show(long gcodeId)`, conservant `Changes`, `Prints`, `AddPrintCommand`, `FullConfig`, `Figures`
- Changes: el toast de gcode nous obre la figura de l'últim gcode, o «Sense figura» si no en té

- [ ] **Step 1: Porta el detall d'intent** de `HistoryView.xaml` al panell: dades, «Què vas canviar», impressions i configuració completa plegada. Elimina `Entries`, `Search` i `Marks`.

- [ ] **Step 2: «Esborrar» una impressió** demana confirmació amb `MessageBox`, perquè no hi ha desfer i pot portar nota i foto.

- [ ] **Step 3: Elimina les pestanyes.** Treu `ShowViewer`, `ShowHistory`, `ShowHistoryNow` i `OpenEntry`. «Assignar model» queda a «Sense figura» de Col·lecció.

- [ ] **Step 4: Comprova-ho** amb una còpia de les dades: afegir, valorar, anotar, posar foto i esborrar una impressió, reiniciar i confirmar que persisteix.

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit i PR**

```bash
git add src
git commit -m "feat: porta els intents a la pantalla Figura"
git push -u origin feat/pantalla-figura
gh pr create --fill
```

---

# Fase 5: comparació

Branca: `feat/comparar-intents`.

### Task 9: Comparar amb…

**Files:**
- Create: `src/LayerLog.Core/Profiles/AttemptComparison.cs`, `src/LayerLog.App/Viewer/ComparisonView.xaml`, `ComparisonView.xaml.cs`
- Modify: `src/LayerLog.App/Viewer/AttemptViewModel.cs`, `src/LayerLog.App/Viewer/ViewerView.xaml`
- Test: `tests/LayerLog.Core.Tests/AttemptComparisonTests.cs`

**Interfaces:**
- Produces: `record ComparedSetting(string Key, string Label, string A, string B)`
- Produces: `AttemptComparison.Between(GcodeEntry a, GcodeEntry b) -> (IReadOnlyList<ComparedSetting> Known, IReadOnlyList<ComparedSetting> Rest)`
- Produces: `AttemptViewModel.CompareWithCommand`, `CloseComparisonCommand`, `CompareChoices`

- [ ] **Step 1: Escriu les proves que fallen**

```csharp
[Fact] public void Known_settings_come_first_and_the_rest_apart()
[Fact] public void Same_gcode_has_no_differences()
[Fact] public void Setting_missing_on_one_side_is_shown_as_missing()
// una clau només a A -> B == "—", mai cadena buida
```

- [ ] **Step 2: Executa i comprova que fallen**

Run: `dotnet test --filter AttemptComparisonTests`
Expected: FAIL, no compila.

- [ ] **Step 3: Mou la lògica** de `Compare()` a Core i implementa `Between`, reutilitzant `ConfigDiff.Between` i `ConfigLabels`.

- [ ] **Step 4: Construeix la vista i el selector.** El selector mostra primer els intents de la figura i després tots els altres gcode. La comparació ocupa la zona de l'escena; Esc la tanca. Mantén la fila «Millor valoració».

- [ ] **Step 5: Comprova-ho.** Run: `dotnet test`, Expected: PASS. Compara Totoro amb TotoroBig i confirma les dades reals: altura 0,08 i 0,16 mm, parets 4 i 2, paret exterior 25 i 60 mm/s.

- [ ] **Step 6: Commit i PR**

```bash
git add src tests
git commit -m "feat: compara intents des de la figura"
git push -u origin feat/comparar-intents
gh pr create --fill
```

---

# Fase 6: tancament

Branca: `feat/tancament-ciclorama`.

### Task 10: Teclat, estats i moviment

**Files:**
- Modify: `src/LayerLog.App/MainWindow.xaml`, `src/LayerLog.App/MainWindow.xaml.cs`, `src/LayerLog.App/Collection/CollectionView.xaml`, `src/LayerLog.App/Viewer/ViewerView.xaml`, `src/LayerLog.App/MainViewModel.cs`

- [ ] **Step 1: Dreceres de la spec.** Ctrl+K cerca, Esc torna enrere o buida la cerca, fletxes i Retorn a la graella, Ctrl+Retorn envia a Creality Print.

- [ ] **Step 2: Estats pendents.** Importació de l'SD en curs i finalitzada a la barra, amb `LiveSetting=Polite`; sense carpeta; 3MF sense vista prèvia a la targeta i a l'escena.

- [ ] **Step 3: Transició** curta de Col·lecció a Figura, un sol cop, desactivada si `SystemParameters.ClientAreaAnimation` és fals.

- [ ] **Step 4: Comprova-ho** fent tota la feina principal sense ratolí, i amb les animacions de Windows desactivades.

- [ ] **Step 5: Commit**

```bash
git add src
git commit -m "feat: completa el teclat, els estats i el moviment"
```

### Task 11: Comprovació final i documentació

**Files:**
- Modify: `README.md`, `docs/layerlog.gif`

- [ ] **Step 1: Prova manual amb SD real**, amb les dades reals fetes còpia abans: connecta la targeta i confirma que els gcode nous apareixen a la figura correcta i que no es duplica res.

- [ ] **Step 2: Rendiment.** `Totoro2.stl` gira amb fluïdesa, i una carpeta de 1.000 models de prova es desplaça sense bloquejos.

- [ ] **Step 3: Executa `/impeccable critique`.** Expected: més de 32/40.

- [ ] **Step 4: Actualitza el README i el GIF,** sense cap dada de mostra sense etiquetar.

- [ ] **Step 5: Commit i PR**

```bash
git add README.md docs/layerlog.gif
git commit -m "docs: actualitza el README amb Ciclorama"
git push -u origin feat/tancament-ciclorama
gh pr create --fill
```
