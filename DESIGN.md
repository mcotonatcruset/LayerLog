# Disseny

Sistema visual de LayerLog tal com està construït. Els tokens viuen a `src/LayerLog.App/Theme.xaml`, i l'escena a `src/LayerLog.App/Viewer/ViewerView.xaml.cs`.

## Tesi: Ciclorama

Un estudi de fotografia. La figura està sobre un fons continu il·luminat, i l'eina queda al costat.

## Colors

| Token | Valor | Ús |
|---|---|---|
| `Sweep` | `#233D47` | Fons de la finestra i vores de l'escena |
| `Panel` | `#142731` | Camps de text i caselles |
| `Raised` | `#2A4048` | Element seleccionat i botons secundaris |
| `Line` | `#3A555E` | Vores, barres de desplaçament, estat premut, pestanya i filtre actius |
| `Ink` | `#EFEBE2` | Text principal i anell de focus |
| `Muted` | `#A9BABD` | Text secundari |
| `Soft` | `#C4D0D1` | Text secundari sobre targetes i a la barra de títol |
| `Card` | `#2A4752` | Targetes de carpeta |
| `CardGlow` | `#55808D` a `#2F4F5B` | Targetes de Recents, degradat radial |
| `Faint` | `#98ABAF` | Text de controls desactivats |
| `Hover` | `#14FFFFFF` | Capa en passar el ratolí |
| `Change` | `#F2B544` | Valoració i canvis de l'usuari, res més |
| `Action` | `#EFEBE2` | Fons del botó principal i de les caselles marcades |
| `ActionText` | `#142731` | Text del botó principal i de les caselles marcades |

Ivori de les figures: 0,9 / 0,87 / 0,8 (`ModelThumbnail.Ivory`). El comparteixen el visor 3D i les miniatures.

## Regles

- **Regla d'acció:** el color que destaca una acció no s'usa per a res més. `Action` és un fons ivori amb text fosc, al botó principal i a les caselles marcades.
- **Regla de canvis:** el perfil base és el fons. Només els canvis de l'usuari i la valoració fan servir `Change`.
- La valoració sempre porta text o estrelles, mai només color.
- La miniatura verda de Creality només identifica un gcode. Les figures sempre són ivori.

## Tipografia

Hanken Grotesk, inclosa a l'app (`Fonts/`), amb Segoe UI de reserva. Xifres tabulars a tota la finestra.

| Estil | Mida | Pes |
|---|---|---|
| `Title` | 34 | Bold |
| `H1` | 22 | SemiBold |
| `H2` | 15 | SemiBold |
| Text | 14 | Regular |
| `MutedText`, botons d'enllaç | 13 | Regular |

Els glifs dels botons de la finestra són de Segoe MDL2 Assets, a mida 10.

## Espais i formes

- Barra de títol: 52 px, amb arrossegament a tota l'amplada i 6 px de vora per redimensionar.
- Botons de la finestra: 46 × 52 px, tocant la cantonada. En maximitzar, l'arrel compensa el marc que surt de la pantalla.
- Separació lateral de la barra: 20 px. L'escena deixa 12 a 24 px fins a la vora.
- Radis: 4 px als camps, 5 a 6 px a botons i files, 8 px als avisos.
- Botons: farciment de 16 × 8 px. Pestanyes: 12 × 5 px.

## Escena

- **Fons continu:** una imatge de 1920 × 1080 calculada un cop, de `#233D47` a les vores a `#4C717D` al centre, amb soroll triangular d'un nivell perquè el degradat no faci bandes. Es dibuixa darrere del model amb `ScreenQuadModel3D` i mostreig puntual, per conservar el soroll a qualsevol mida.
- El fons és fix a la pantalla, perquè la càmera orbita: una paret dins l'escena giraria amb ella. Si s'inclina o es desplaça la vista, el fons no es mou.
- **Ombra de contacte:** un pla amb una textura radial negra sota la figura, de la mida de la seva base. Gira amb la figura.
- **Enquadrament:** una figura tan alta com ampla ocupa més del 60 % de l'alçada de la vista.
- Els missatges del centre de l'escena (càrrega, error, 3MF) van sobre una píndola opaca de `Panel`, que només apareix quan hi ha alguna cosa a dir.
- L'escena no porta cap text directament a sobre, tret de la barra de títol i de la píndola de missatges.
- La barra de títol és transparent sobre l'escena, i per això el text no seleccionat és `Soft`: 5:1 a la zona més clara del fons, i 7,3:1 sobre `Sweep`.

## Col·lecció

- Una llista virtualitzada de files de targetes: cada fila és un `ListBox` d'una sola línia, i les columnes surten de l'amplada, amb un mínim de 240 px per targeta.
- Targetes de Recents: fons `CardGlow`, imatge de 250 px. Targetes de carpeta: fons `Card`, imatge de 130 px a l'esquerra. Separació de 16 px, radi de 10 px.
- Les miniatures es descodifiquen a la mida en què es mostren, multiplicada pels DPI i amb un màxim de 320 px.
- La figura oberta porta una vora `Muted` de 2 px. El focus és l'anell `Ink`, 3 px per fora.
- Clic o Retorn obren la figura. Espai la selecciona per enviar-la. F2 li canvia el nom. P la marca o desmarca com a peça. La casella només apareix en passar el ratolí, amb focus o si està marcada.
- Les files de «Sense figura» obren el gcode al panell d'intent. Sense carpeta de models també es poden obrir: la llista és només aquesta, amb el missatge i «Triar una carpeta» a sobre.
- Les fletxes recorren files i seccions: amunt i avall visiten cada fila amb alguna cosa per enfocar (targetes, capçaleres de carpeta i gcodes sense figura) i recorden la columna; esquerra i dreta passen de targeta en targeta i salten a la fila següent. Una fila fora de la vista es porta a la vista abans d'enfocar-la.
- En tornar d'una figura, el focus va a la seva targeta, amb preferència per la còpia de la carpeta sobre la de Recents. Si s'ha tancat un gcode sense figura, va a la seva fila. En marcar una peça que deixa de veure's, va a la targeta veïna.
- La barra d'enviament és `Panel`, a la part inferior, i només hi és amb alguna figura seleccionada.

## Panell de figura

- 390 px d'amplada, opac (`Panel`), a la dreta. L'escena ocupa la resta, i per això la figura queda centrada a la zona lliure. Sense figura, el panell desapareix.
- El contingut es desplaça, i «Enviar a Creality Print» queda fix a sota. Marges laterals de 26 px, 22 px entre blocs.
- El nom va en `Title`. La nota de la millor impressió, a 17 px: és contingut, no metadada. Sense nota, el bloc no hi és.
- Canvis clau: etiqueta `Soft`, valor base `Muted` i valor usat `Change`.

## Panell d'intent

- Mateix lloc i amplada que el panell de figura, que substitueix quan s'obre un intent. «Tornar» queda fix a dalt; Esc fa el mateix.
- Ordre: gcode, altres figures, «Què vas canviar», impressions i configuració completa plegada. Els canvis fan servir les mateixes files que el panell de figura.
- Un gcode sense figura s'obre igual, amb l'escena buida i la píndola «Aquest gcode no té cap figura assignada.» amb el botó «Assignar model». En assignar-lo, s'obre la figura.
- La figura oberta i la recordada no canvien: Esc hi torna, o a Col·lecció si no n'hi havia.
- Esborrar una impressió es confirma a la mateixa fila: un text amb el que es perd, i «Cancel·lar» amb el focus. Esc cancel·la.

## Comparació

- Cobreix la columna de l'escena, en `Sweep` opac: la figura en pausa no es transparenta. «Tornar a l'intent» queda a dalt amb el focus; Esc fa el mateix.
- Es comparen dos gcodes de la mateixa figura o de fora, des de «Comparar amb…» al panell d'intent. La llista s'obre amb el focus a la primera opció: fletxes, Retorn i Esc.
- Capçalera amb una miniatura de 72 px per intent, i el títol de l'avís és `LiveSetting=Polite`. Les diferències fan servir les files del panell de figura; les que no canvien es pleguen.
- Si la comparació és aproximada o falta el perfil base, ho diu amb el mateix avís de sempre.
- En tancar-la, el focus torna a «Comparar amb…».

## Miniatures

320 × 320 px, renderitzades per programari amb l'ivori de les figures i fons transparent. La mida forma part del nom del fitxer, de manera que canviar-la les regenera.

## Interacció

### Teclat

| Tecla | Fa |
|---|---|
| Ctrl+K | Cerca. Des d'una figura torna a la col·lecció |
| Fletxa avall o Retorn a la cerca | Entra a la graella |
| Fletxes a la graella | Es mouen per targetes, capçaleres i gcodes sense figura |
| Retorn | Obre la targeta, la fila o l'intent |
| Espai | Selecciona la targeta per enviar-la |
| P | Marca o desmarca la peça |
| F2 | Canvia el nom |
| Ctrl+Retorn | Envia a Creality Print: la figura oberta, les seleccionades, o la de la targeta amb el focus |
| Esc | Tanca el que hi ha obert: llista de comparació, comparació, intent, figura. A la col·lecció buida la cerca |

- Tab recorre la barra primer, després la pantalla, i torna a començar. Cada pantalla és un grup `TabNavigation="Local"`; la vista 3D no és parada de tabulador.
- Cap control porta el rectangle puntejat per defecte de WPF: tots fan servir `FocusRing` o la seva variant.

- Focus de teclat visible a tots els controls: anell `Ink` de 2 px. Als botons de la finestra l'anell va per dins, perquè toquen la vora. Els camps de text i les files de l'arbre no fan servir l'anell: mostren el focus amb una vora `Ink`.
- Tots els botons de la finestra tenen nom accessible en català: Minimitzar, Maximitzar o Restaurar, i Tancar.
- La rotació automàtica no s'activa si Windows demana reduir les animacions.

### Moviment

- Una sola transició, de Col·lecció a Figura: la vista de la figura apareix en 160 ms (`DoubleAnimation` d'opacitat, `QuadraticEase`). No s'anima en tornar, ni en obrir la figura recordada a l'arrencada.
- Si `SystemParameters.ClientAreaAnimation` és fals, no hi ha transició ni rotació.

### Estats

- Sense carpeta: el missatge i «Triar una carpeta». Carpeta buida, cap resultat de la cerca, figura sense intents: cadascun amb el seu text.
- 3MF: a la targeta es queda l'etiqueta del tipus de fitxer; a l'escena, la píndola «La previsualització de 3MF encara no està disponible.»
- Importació de l'SD: la barra diu «SD ... · important…», després «importada» o «importació interrompuda», fins que es retira la targeta. És `LiveSetting=Polite`, igual que els avisos.
