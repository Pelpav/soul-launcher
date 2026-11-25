# Soul Launcher

Lanceur de sauvegardes pour **UNDERTALE** (PC, v1.08) : choisis un point de départ (juste avant Sans, Omega Flowey, Undyne l'Immortelle…), règle tes stats et ton inventaire, et le jeu démarre directement là.

> Outil de fan **non officiel**, ni affilié à ni approuvé par Toby Fox. Il ne contient aucun fichier du jeu : il faut posséder UNDERTALE.

## Fonctions
- 172 points de départ : 7 intégrés (Flowey's Time Machine) + 165 saves communautaires (crédits dans `packs/SOURCES.md`).
- Recherche, favoris, récents, aperçu du preset (lieu, route, stats, objets).
- Import de ta propre partie, partage de saves en fichier `.soul`.
- Détection automatique du jeu (Steam, GOG, Xbox, dossiers habituels).
- Annule la fin génocide (écran noir), y compris dans le Steam Cloud.
- Copie de ta vraie sauvegarde avant chaque remplacement, bouton pour la restaurer.
- Français / English, mises à jour automatiques.

## Télécharger
Dernière version : [Releases](../../releases/latest) → `SoulLauncher.exe`.
Windows peut afficher « Windows a protégé votre ordinateur » (exe non signé) : **Informations complémentaires → Exécuter quand même**.

## Compiler
Windows suffit (csc.exe de .NET Framework 4.x) + Python 3 pour empaqueter les packs :

```powershell
.\build.ps1
```

## Publier une mise à jour

```powershell
.\release.ps1 1.0.1 "Ce qui change"
```
