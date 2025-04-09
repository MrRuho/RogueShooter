# RogueShooter - Suunnitteludokumentti

## Pelimekaniikka:
- Pelaajat ohjaavat erikoisjoukkojen tiimiä
- Vuoropohjainen toiminta (XCOM-tyyliin)
- Tavoitteena on puhdistaa rakennus vihollisista
- Satunnaisesti luotu kampanja kartta.
- Kampanjassa pelaaja joutuu tekemään valintoja etenemisen suhteen.( Varusteiden huolto, Lisää ammustarvikkeita, lepo, tutki rakennus jne.)(Esim. Slay the Sprite tyylinen kartta)
- Hahmoilla voi olla erikois kykyjä, esim. poikkeuksellisen nopea juoksija. Pokkauksellisen hyvä jossakin aseessa jne. (Ei ostettavissa)
- Hahmot keräävät kokemusta ja niille voi ostaa yleisiä taitoa
- Hahmon kuolema on lopullista
- Varusteiden menettäminen on lopullista
- Tuhoutuvat ympäristöt
- Pelaaja näkee vain sen mitä hahmot näkevät
- Pelaaja voi yrittää perääntyä epätoivoisesta taistelusta. Peli päättyy tappioon mutta seuraavaa koitosta varten, pelaaja saa mukaansa haluamansa sotilaat.

## Taktiikat ja kyvyt:
    - Vastustajan liikkeen kontrollointi ja pakottaminen. Agressiivisella tulella voidaan pyrkiä saamaan vastustaja liikkumaan
    haluttuun paikkaan.

        Esim. Tuli on keskitetty yhden vihollisen ympärille sekä viereisiin suojiin, jolloinka vastustaja ei
        pysty vain liikkumaan vireiseen suojaan ja saamaan suojapisteitään takaisin, vaan hänen on siirryttävä tulialueelta kokonaan
        pois. Tämä voi johtaa ns tappoalueelle eli vaikka tarkka-ampujan tulilinjalle.

        Kuinka tämä käytännössä toimii?
        Jokaisella yksiköllä on tulikontrollitoiminto, joka on hyvin epätarkkaa tulta ja levittäytyy useammalle ruudulle. Yksi yksikkö voi ampua suoraan siihen kohtaan missä vastustajan yksikkö on ja pari muuta voivat ampua viereisiin ruutuihin. Vaikka ne eivät voi tehdä edes teoriassa vahinkoa, niin ne sulkevat vastustajan yksikön mahdollisuuden liikkua viereiseen suojaan saadakseen suojapisteet takaisin. Näin vastustaja pakotetaan liikkumaan itselle mieleiseen suuntaan.

        ++ Estää vastustajaa samaan suojapisteitä takaisin
        + Yksikkö/yksiköt joka on vaikutuksen alla menettää aina suojapisteitä
        - Pieni osumamahdollisuus.
        -- Äärimmäisen pieni kriittinen osumamahdollisuus

    - Täysi suojautuminen. Käytä koko vuoro rauhallisessa paikassa ja palauta kaikki suojapisteet.


## Aseet ja taistelu:
- Pelaajat voivat käyttää erilaisia aseita (pistooli, haulikko, jne.)
- Aseilla on rajallinen määrä ammuksia
- Aseilla on oma optimi kantamansa
- Liikkuminen ja ammunta tapahtuu vuoropohjaisesti
- Hahmoilla on vain yksi kestopiste, sekä läjä suojapisteitä. Suojapisteet kasvavat ja kuluvat dynaamisesti. Hahmo menettää aina ensin suojapisteitä ja vasta sitten kestopisteen
joka vastaa osumaa. Vihollistulen alla, suojapisteitä kuluu aina vähintään yksi suojapiste. Kestopisteen menetys vaatii kuitenkin aina oikean osuman.
- Suojapisteitä saa takaisin liikkumalla tulialueelta pois.(Mekaniikan tarkoitus on kannustaa liikkumaan ja lisätä tulen käytön määrää)
- Kriittinen osuma menee aina suojapisteiden läpi.
- Panssarointi antaa pienen mahdollisuuden sille, että kuolettava osuma onkin vain kriittinen, tai ei mene läpi ollenkaan.
- Taistelukentän tavarat ja muurit toimivat kuten panssarointi.
- Hahmoilla saattaa olla sankarillisia uhraus toimintoja. Esim. Yksi toiminto ennen lopullista kuolemaa. ( Saa mahdollisuuden kostaa) Viimeisenä tekona vetää kranaatista sokan irti.)

## Vihollis-AI:
- Viholliset voivat partioida huoneissa
- Kun pelaaja havaitaan, viholliset hyökkäävät
- Vihollinen tekee jotakin odottamatonta.
- Vihollinen saattaa luovuttaa ja perääntyä jos tappiosuhde on liian suuri

## Grafiikka, äänet ja animaatio 
- Grafiikka Synty Studios assetit
- Äänet. Valmiit äänipaketit. Ai generoitua musiikkia
- Animaatiot. Valmiit animaatio paketit
- Näyttäviä lopetus liikkeitä. Esim sniper head shot näytetään hidastetusti. Tarkka-ampuja lataa aseensa ja hylsy lentää ulos pesästä. "The enemy has been eliminated."


## Camppain Events
- High risk high revard:
    esim. Löydät hyväkuntoisia varusteita. Niissä voi olla ansa ja pahimmassa tapauksessa sotilas kuolee.
- Randomisti luodut kokonaiset peli kamppanjat.

## Tehtävät tyypit
    



