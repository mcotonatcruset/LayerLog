# LayerLog: disseny

Data: 2026-10-31. Estat: pendent d'aprovació.

## Per què existeix

LayerLog és una app d'escriptori per a Windows en C#, pensada per dues coses:

1. **Una necessitat real de l'autor:** el que més li ha costat és trobar la configuració que fa que certes figures surtin amb detall. Avui aquest coneixement es perd entre perfils de Creality Print que van canviant i gcode solts a les SD.
2. **Una mostra pública per al CV:** l'autor busca feina de desenvolupador d'escriptori amb C#. El seu codi professional no es pot ensenyar i li falta experiència visible en una tecnologia d'escriptori moderna.

La idea en una frase: un visor 3D de la teva carpeta de models amb un historial que recorda amb quina configuració es va imprimir cada figura.

## Objectius

- Veure qualsevol figura de la carpeta en un visor 3D, sense passos intermedis.
- Enviar una figura o un grup de figures a Creality Print.
- Construir l'historial automàticament a partir dels gcode de les SD.
- Consultar, per a cada impressió, la configuració usada i què es va canviar.

## Fora d'abast

- **Estimacions de temps, filament i cost pròpies.** Creality Print ja les fa. Si el gcode porta aquestes dades, es poden mostrar, però l'app no en calcula cap.
- **Laminar.** Tot el laminat es fa a Creality Print.
- **Controlar la impressora.** La impressora imprimeix des de SD. Res d'USB, OctoPrint ni seguiment en viu.
- **Altres sistemes operatius i altres laminadors.**
- **Sincronització al núvol i comptes d'usuari.**

## Què el faria un fracàs encara que funcionés

- Que perdi entrades de l'historial o en dupliqui en connectar una SD.
- Que modifiqui o esborri res a la SD, a la carpeta de models o als fitxers de Creality Print.
- Que la interfície es bloquegi mentre carrega un model o llegeix una SD.
- Que el codi públic no es pugui defensar en una entrevista: ha de ser codi que l'autor entengui.

## Decisions de l'autor

- **Visor:** és la pantalla principal.
- **Carpeta:** hi ha una carpeta per defecte. Si es canvia, es guarda.
- **Última figura:** en obrir l'app, es torna a mostrar l'última figura oberta.
- **Explorador:** va a l'esquerra, a dins del visor, i no en un contenidor a fora. El fons és transparent i es veu l'escena 3D a través.
- **Enviar a imprimir:** una figura o un grup van a Creality Print.
- **Registre:** quan es crea el gcode, queda a l'historial amb la configuració usada.
- **SD:** el destí és sempre `D:\`. Hi ha dues targetes, sense carpetes de moment, però en un futur en pot haver.
- **Més seccions:** es poden afegir més endavant.
- **Gcode i impressions:** un gcode pot tenir cap, una o diverses impressions, cadascuna amb el seu resultat, nota i foto.
- **Historial al visor:** en seleccionar una figura, el visor mostra com s'ha imprès abans.
- **Abast dels canvis:** es comparen els paràmetres de procés i de filament, no els de la impressora.

## Decisions preses per Claude (revisables)

- **Tecnologia:** WPF sobre .NET 10 (LTS). És el més demanat a les ofertes d'escriptori amb C# i és el pas natural des de Windows Forms.
- **Base de dades:** SQLite. L'historial queda en un sol fitxer local, a `%LOCALAPPDATA%\LayerLog`.
- **Carpeta per defecte a la primera obertura:** `C:\Users\marti\Documents\Impresora3D` si existeix. Si no, es demana.
- **Identitat d'un model:** es guarda el camí relatiu a la carpeta i el hash del fitxer. Si un model es mou o es reanomena, el hash permet retrobar l'enllaç.
- **Millor configuració d'una figura:** és la de la impressió amb la valoració més alta i, en cas d'empat, la més recent.
- **Comparar dues entrades de l'historial:** inclòs a la primera versió, dins de la secció Historial.
- **Dues seccions a la primera versió:** Visor i Historial.
- **Què es guarda de cada gcode:** la configuració, les dades de capçalera, la miniatura i el hash, però no el gcode sencer, perquè cada fitxer pot ocupar més de 20 MB.

## Comportament

### Visor

- L'escena 3D ocupa tota la finestra.
- L'explorador mostra l'arbre de carpetes i els models (STL i 3MF) a sobre de l'escena.
- En seleccionar un model, es carrega en segon pla i es mostra un indicador de càrrega. La interfície continua responent.
- Si un fitxer no es pot llegir, es mostra un missatge i l'app continua funcionant.
- La mida de referència és `Totoro2.stl`: 52 MB i 1,05 milions de triangles. S'ha de poder girar amb fluïdesa.
- Un panell a sobre de l'escena mostra les impressions de la figura seleccionada i la seva millor configuració, amb accés a l'entrada de l'historial. Si no té impressions, ho indica.

### Enviar a Creality Print

- L'usuari selecciona una o més figures i prem Enviar.
- L'app obre `CrealityPrint.exe` amb els camins com a arguments.
- **Provat el 2026-10-31:** amb diversos fitxers, Creality Print mostra el missatge "¿Cargar estos archivos como un objeto único con múltiples piezas?". Cal respondre **No** perquè quedin com a objectes separats. L'autor ho ha comprovat. L'app ha d'avisar d'això abans o en el moment d'enviar un grup.
- Cada enviament obre una finestra nova de Creality Print, perquè `single_instance` és `false`.
- La ruta de l'executable no s'ha de donar per fixa. Actualment és `C:\Program Files\Creality\Creality Print 7.0\CrealityPrint.exe`, tot i que la versió real és posterior. Si no es troba, es demana a l'usuari i es guarda.

### Importar des de la SD

- En connectar una unitat extraïble, o amb l'app ja oberta amb la unitat connectada, es busquen gcode a tota la unitat, també a dins de carpetes.
- Si l'app està oberta mentre s'exporta a la SD, el gcode nou apareix sense haver de tornar a connectar-la.
- La detecció no pot dependre d'estar oberta en el moment d'exportar. Els fitxers es detecten igualment la propera vegada que es connecti la targeta.
- Les targetes es distingeixen pel número de sèrie del volum, perquè totes dues són `D:\`. Serials actuals: `DE2F-F964` i `9016-4EF8`.
- Els duplicats es detecten pel hash del contingut, no pel nom.
- Els fitxers s'han d'acceptar encara que el mateix gcode aparegui a les dues targetes: és una sola impressió, present a dos llocs.
- L'accés a la SD és només de lectura.
- Els 3MF que hi hagi a la SD (n'hi ha un, `_TotsJunts.3mf`) no generen entrades.

### Historial

Cada entrada és un gcode importat i mostra:

- Miniatura, nom del fitxer, data i versió de Creality Print.
- A quina targeta és, o si ja no és a cap.
- Les figures que conté, enllaçades amb els models de la carpeta quan el nom coincideix.
- Els perfils de procés i filament usats i **només els paràmetres que difereixen del seu perfil base**, separats per categoria, amb la configuració completa desplegable.
- Les seves impressions: cadascuna amb data, resultat, nota i foto.

Importar un gcode no crea cap impressió, perquè exportar-lo no vol dir haver-lo imprès. Els gcode sense impressions es veuen com a "no imprès" fins que l'autor n'afegeix una. L'historial es pot filtrar per figura.

Una entrada es manté encara que el gcode s'esborri de la SD. Si es torna a veure el mateix contingut, no es duplica.

Si el nom de l'objecte no permet enllaçar amb cap model, com `1.stl`, l'entrada es queda sense enllaç i l'usuari la pot assignar a mà. Si hi ha diversos fitxers amb el mateix nom, l'app no n'escull cap per si sola.

## Disseny visual

Aprovat per l'autor el 2026-10-31 a partir d'un prototip web local, a `prototype/`, que no es publica. El prototip és la referència visual, però no s'aprofita com a codi. El to i els principis són a `PRODUCT.md`.

- **Tema:** fosc, com l'interior d'una vitrina. El fons és gairebé negre i neutre (`oklch(0.1 0 0)`).
- **Color:** tot neutre, amb un sol vermell (`oklch(0.56 0.21 5)`) per a la selecció i l'acció principal. El text sobre vermell és blanc, i el hover del botó s'enfosqueix per mantenir el contrast. El vermell en text sobre fons fosc és més clar (`oklch(0.76 0.15 8)`).
- **Figura:** to marfil, llum focal des de dalt i un cercle de llum al terra. Gira lentament fins que l'usuari la toca, excepte si Windows té les animacions reduïdes.
- **Tipografia:** Segoe UI Variable, una sola família, amb xifres tabulars a les dades.
- **Moviment:** transicions de 120 a 250 ms, només per indicar canvis d'estat.

**Visor**

- L'escena ocupa tota la finestra, i la barra superior també flota a sobre, amb LayerLog, les pestanyes Visor i Historial i l'estat de la SD.
- L'explorador és a l'esquerra i només és text sobre l'escena, amb una ombra de text per llegir-se. La casella per marcar una figura apareix en passar el ratolí.
- A la dreta hi ha el nom, les mides, la millor configuració i les impressions de la figura.
- A baix a la dreta hi ha el botó Enviar a Creality Print. Amb dues o més figures, el botó indica quantes són i apareix l'avís de respondre No.

**Historial**

- A l'esquerra, una llista de gcode amb cerca: miniatura, nom, data, perfil i estat, sense targetes.
- A la dreta, l'entrada seleccionada es llegeix com una nota: capçalera, Què vas canviar (perfil base i valor usat, per perfil), Impressions i la configuració completa desplegable.
- Comparar: es marquen dues entrades i es mostren només les diferències en dues columnes, amb els noms traduïts a dalt i la resta desplegable.

**Estats:** cal mantenir l'estat buit de l'historial, la figura encara no impresa, la càrrega del model amb barra de progrés, l'error en llegir un fitxer, la comparació aproximada i l'avís de SD nova.

## Dades comprovades als fitxers reals

S'han revisat els 9 gcode de les dues SD, generats amb Creality Print 7.0, 7.1 i 7.3:

- **Tots** tenen un bloc `; CONFIG_BLOCK_START` ... `; CONFIG_BLOCK_END` al final del fitxer, amb línies `; clau = valor`, i un identificador `creality_uuid` a la capçalera.
- La data i la versió surten de la línia `; generated by Creality_Print V... on ... at ...`. Es fa servir aquesta data, no la del fitxer.
- Les figures surten a les línies `; printing object <nom> id:<n> copy <k>`, repetides per capa. Cal treure'n els noms únics. Hi pot haver objectes interns com `Cubo`.
- La miniatura és un PNG en base64 entre comentaris (`; png begin ...`).
- La línia inicial `;Layer height:` **no** és fiable: a `KatanaStand.gcode` diu 0,200 quan el perfil real és 0,16. Cal fer servir el bloc de configuració.
- La configuració del perfil canvia amb el temps. Per exemple, el mateix perfil personalitzat dona capa 0,1 a `MarineParts.gcode` i 0,08 a `Totoro.gcode`. Per això cada entrada guarda la seva pròpia còpia dels valors.
- Els perfils de sistema són a `resources\profiles\Creality\process\` a la instal·lació, i a `%APPDATA%\Creality\Creality Print\7.3\system\Creality\process\`. Els personalitzats són a `...\7.3\user\default\` i tenen un camp `inherits` amb el perfil de sistema del qual parteixen.

El bloc de configuració té uns 574 paràmetres barrejats de procés, filament i impressora, i `inherits_group` indica els perfils pare. Quan el perfil de procés és de sistema, la seva posició a `inherits_group` queda buida: a `TotoroBig.gcode`, per exemple, cal agafar `print_settings_id`. Per calcular els canvis, el valor base surt dels perfils de sistema de procés i de filament, resolent la cadena `inherits` de cadascun. Els paràmetres d'impressora no es comparen. Els gcode antics poden no coincidir amb els perfils de sistema: per exemple, no hi ha perfils de la versió 7.1 al disc. Decisió de Claude, revisable:

- Es fa servir el perfil de sistema de la mateixa versió si existeix. Si no, el de la versió disponible més propera, i l'entrada indica que la comparació és aproximada.
- Si no hi ha cap perfil amb aquell nom, l'entrada mostra la configuració completa i ho indica.
- Els valors base es guarden amb l'entrada en importar-la, perquè una actualització de Creality Print no canviï els resultats antics.

## Demostració per al README

`Totoro.gcode` (capa 0,08, perfil Fine personalitzat) i `TotoroBig.gcode` (capa 0,16, perfil Optimal) són dues impressions de `Totoro2.stl`, totes dues a la targeta `9016-4EF8`. Comparar-les és el cas perfecte per mostrar l'app.

Els models de tercers no s'han de publicar al repositori. El `Totoro2.stl` és d'un altre autor, així que les captures s'han de revisar abans de publicar-les.

## Criteris d'èxit

- Connectar qualsevol de les dues SD crea entrades per a tots els gcode, sense duplicats en tornar-la a connectar.
- Comparar `Totoro.gcode` i `TotoroBig.gcode` mostra les diferències reals de configuració.
- Seleccionar `Totoro2.stl` al visor mostra les seves impressions.
- El parser de gcode i el càlcul de diferències tenen tests automàtics amb fragments de gcode reals.
- El repositori públic té un README amb GIF i instruccions per executar l'app.

## Decisions per a qui ho construeixi

- **Biblioteca 3D (decidida el 2026-10-31):** `HelixToolkit.Wpf.SharpDX` 3.1.2, amb `EnableSwapChainRendering="False"` perquè els controls WPF es puguin compondre a sobre. Amb `Totoro2.stl` a 1500×900 i una RTX 5060 Ti: 240 FPS, càrrega de 0,46 s i 496 MB. El STL es llegeix amb codi propi en segon pla. La pausa més llarga de la interfície va ser de 338 ms, en pujar el model a la GPU.
- L'organització interna del codi, les biblioteques de suport i l'estructura de la base de dades.
- Com es detecta la connexió d'una SD.
- El disseny visual concret, sempre respectant el panell transparent dins del visor.
- Com llegir els 3MF al visor. Si complica massa la primera versió, es pot començar només amb STL i dir-ho clarament.
