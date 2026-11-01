# LayerLog: redisseny Ciclorama

Data: 2026-11-01. Estat: pendent d'aprovació.

Substitueix les parts d'estructura i d'aspecte de `2026-10-31-layerlog-design.md`. La resta d'aquell document continua vigent: què s'importa, com es compara i què està fora d'abast.

Maquetes: [llenç de propostes](https://claude.ai/artifact/S7tkwsaXezKff11KHd7aqh), fila A.

## Per què

La crítica del 2026-11-01 va donar 19/40. El problema principal és que l'app està organitzada per fitxers, i l'usuari pensa en figures. Per saber què va funcionar amb una figura s'ha de saltar entre Visor i Historial. A més, l'app no té identitat: amb una barra de títol blanca i un rosa que ho fa tot, podria ser qualsevol visor fosc.

L'èxit continua sent el de `PRODUCT.md`: trobar què va funcionar per a una figura és qüestió de segons. L'app també ha de demostrar cura de producte a qui obri el repositori.

## Objectius

- La figura és la unitat de l'app.
- De la col·lecció a «què va funcionar» en un clic.
- Una identitat pròpia que es reconegui sense llegir el nom.
- WCAG AA real, amb tota l'app usable amb teclat.
- Cap funció actual es perd.

## Fora d'abast

- Funcions noves d'importació, de comparació de perfils o de laminat.
- Vista prèvia de 3MF.
- Tema clar.
- Fons diferents per carpeta.

## Què el faria un fracàs

- Que la cerca o l'enviament a Creality Print siguin més lents que ara.
- Que deixi de girar amb fluïdesa `Totoro2.stl` (1,05 milions de triangles).
- Que una dada aproximada o que falta es mostri com si fos certa.
- Que sigui un tema de colors a sobre de la mateixa estructura.
- Que es perdi cap gcode, impressió, nota o foto existent.

## Decisions de l'autor

- **Direcció:** A, Ciclorama.
- **Estructura:** Col·lecció + Figura. Desapareix la secció Historial.
- **Abast:** totes les fases.
- Queda substituïda la decisió antiga «l'explorador va a dins del visor, amb fons transparent».

## Vocabulari

| Terme | Significat |
|---|---|
| Figura | Un model de la carpeta |
| Peça | Un model que forma part d'una figura i no es mostra per defecte |
| Intent | Un gcode associat a una figura |
| Impressió | El que ha sortit de la impressora, amb valoració, nota i foto |

Un intent pot no tenir cap impressió. Es diu «Sense imprimir», mai «No imprès» dins d'una llista d'impressions.

## Estructura

### Col·lecció (pantalla inicial si no hi ha cap figura recordada)

- Barra superior: nom de l'app, cerca, filtre (Totes, Impreses, Sense imprimir) i estat de l'SD.
- Secció «Recents»: les figures amb intents més nous.
- Seccions per carpeta, plegables. Canviar de carpeta de models es fa des d'aquí.
- Cada figura: render, nom, valoració, nombre d'intents i millor perfil.
- Selecció múltiple per enviar un grup a Creality Print. L'avís de resposta «No» es manté.
- Secció «Sense figura» quan hi hagi gcode sense model, amb l'acció d'assignar-lo.

### Figura

- L'escena ocupa la part esquerra; a la dreta hi ha un panell opac de 390 px.
- Panell: nom, carpeta i fitxer; «El que va funcionar» amb valoració, nota i canvis clau; altres intents; «Enviar a Creality Print».
- En obrir un intent, el panell mostra les seves dades, els canvis, les impressions i la configuració completa plegada.
- Afegir, valorar, anotar, posar foto i esborrar una impressió es fa aquí.
- Un gcode amb diversos objectes apareix com a intent a cada figura.
- Si l'última figura recordada existeix, l'app s'obre aquí.

### Comparació

- Des d'un intent, «Comparar amb…» obre un selector amb els altres intents de la figura primer i, a sota, tots els altres gcode. Així es manté poder comparar dos gcode qualssevol.
- La vista ocupa la zona de l'escena, amb els paràmetres coneguts primer i la resta plegats.

## Món visual

- **Tesi:** un estudi de fotografia. La figura està sobre un fons continu il·luminat, i l'eina queda al costat.
- **Colors:** petroli de fons, panell #142731, tinta ivori #EFEBE2. Groc #F2B544 només per als canvis i la valoració. Botó principal ivori.
- **Regla d'acció:** el color que destaca una acció no s'usa per a res més.
- **Regla de canvis:** el perfil base és el fons, i els canvis de l'usuari van en un color reservat.
- **Tipografia:** Hanken Grotesk, inclosa a l'app. Xifres tabulars. Títols de figura a 34 px.
- **Barra de títol pròpia** amb `WindowChrome`, sobre el mateix fons.
- **Gest propi:** l'escena es renderitza com un fons continu, amb un focus suau i una ombra de contacte sota la figura. Substitueix el terra actual, el fons pla i els degradats laterals.
- **Figures:** les miniatures de la col·lecció fan servir el mateix material ivori. La miniatura verda de Creality només apareix identificant un gcode.
- **Moviment:** curt i un sol cop, en passar de Col·lecció a Figura. Desactivat si Windows ho demana.
- La valoració sempre porta text o estrelles, mai només color.

## Teclat

- Focus visible a tots els controls.
- Ctrl+K: cerca. Esc: tornar enrere o buidar la cerca.
- Fletxes a la graella i Retorn per obrir.
- Ctrl+Retorn: enviar a Creality Print.

## Estats

- Sense carpeta, carpeta buida, cap resultat i cap figura sense intents.
- Càrrega de model, error de lectura i 3MF sense vista prèvia.
- Importació de l'SD en curs i finalitzada.
- Comparació aproximada i perfil base que falta, amb el mateix avís que ara.

## Decisions preses per Claude (revisables)

- **Noms de figura:** per defecte, el nom del fitxer. Es poden editar. Nova taula `figure_meta(relative_path, model_hash, alias, is_part)`, que es reenllaça igual que els objectes de gcode quan un model es mou.
- **Peces:** es marquen a mà, amb selecció múltiple. No hi ha cap heurística sobre noms, perquè endevinar-ho incompliria «mai inventis dades». Per defecte tot és figura.
- **Graella:** virtualitzada, per suportar carpetes molt més grans que les 72 actuals.
- **Renderer del fons:** s'intenta primer amb materials de Helix; si no flueix, es fa servir una imatge de fons pre-renderitzada.
- **Sense llista global de gcode:** «Recents», «Sense figura» i el selector de comparació cobreixen el que feia l'Historial.

## Queda per al constructor

- Mides exactes, ombres i corbes de moviment dins de les regles de dalt.
- La longitud d'animació, que no pot alentir la navegació.
- Com es divideix el codi de `ViewerViewModel` i `HistoryViewModel`.

## Fases

Cada fase és una PR que es pot provar sola.

1. **Vocabulari i dades:** `figure_meta`, consultes per figura i proves a Core.
2. **Base visual:** tokens, tipografia, barra de títol, focus i `DESIGN.md`.
3. **Col·lecció.**
4. **Figura:** escena, panell, intents i impressions. La secció Historial desapareix aquí.
5. **Comparació.**
6. **Tancament:** dreceres, estats, moviment, GIF i README.

## Com sabrem que està fet

- De Col·lecció a la millor configuració de Totoro en un clic.
- Totes les funcions del README funcionen, amb les dades existents intactes.
- Tota la feina principal es pot fer sense ratolí.
- `Totoro2.stl` gira amb fluïdesa.
- La crítica supera 32/40.
