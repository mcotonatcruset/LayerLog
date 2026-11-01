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
| `Line` | `#3A555E` | Vores, barres de desplaçament, estat premut |
| `Ink` | `#EFEBE2` | Text principal i anell de focus |
| `Muted` | `#A9BABD` | Text secundari |
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
- L'explorador i l'etiqueta de la figura encara floten sobre l'escena amb `SceneShadow`. És provisional: sobre la part clara del fons aquest text no arriba a AA fins que passi a una superfície opaca.

## Miniatures

320 × 320 px, renderitzades per programari amb l'ivori de les figures i fons transparent. La mida forma part del nom del fitxer, de manera que canviar-la les regenera.

## Interacció

- Focus de teclat visible a tots els controls: anell `Ink` de 2 px. Als botons de la finestra l'anell va per dins, perquè toquen la vora. Els camps de text i les files de l'arbre no fan servir l'anell: mostren el focus amb una vora `Ink`.
- Tots els botons de la finestra tenen nom accessible en català: Minimitzar, Maximitzar o Restaurar, i Tancar.
- La rotació automàtica no s'activa si Windows demana reduir les animacions.
