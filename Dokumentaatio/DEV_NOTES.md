# DEV_NOTES.md – Kehittäjän muistiinpanot

Tämä tiedosto sisältää tärkeää teknistä tietoa projektin kehityksestä, joka ei välttämättä näy suoraan koodissa tai suunnitteludokumentissam kuten Unity Editorin asetuksia, järjestelmän erityispiirteitä ja muuta kontekstisidonnaista tietoa.

## Project Settings: Script Execution Order
- `UnitActionSystem.cs` on nostettu korkealle prioriteelitte, jotta varmistutaan siitä, että 
Pelaajan yksiköt luodaan ennen kuin mitään muuta pelikoodia ajetaan.

## Unity Project Settings -muutokset


## Debug-tiedot ja testaus


## Relay- ja verkkoyhteydet
- Jos Relay ei toimi, tarkista:
  - Onko pelaaja kirjautunut sisään (`Authentication.cs`)
  - Onko `useRelay = true` määritetty oikein ennen yhteyden luontia

## Muuta muistettavaa
- Yksiköt käyttävät animaatioissa avataria `Mixamo_Unity_POlYGON_Guy_NakedAvatar`
  - Todettu ainoaksi toimivaksi (11.4.25)