# Postiz.NET - Istruzioni e policy progetto

## Template di origine

- Template: `dotnet-library` (Libreria .NET (NuGet))
- Tipo: package
- Descrizione: Class library .NET con packaging NuGet: src/, tests/, samples/, CI dotnet pack.

## Stato iniziale

Repo e cartella locale preparate da GitHub-Manager:

- branch `main` (stabile) e `dev` (sviluppo, default) gia esistenti;
- ruleset di protezione gia configurati su entrambi i branch;
- struttura cartelle predisposta dal template, senza sorgenti;
- manifest base presente (README, CHANGELOG, Docs/, .memory/, .gitignore).

## Struttura predisposta

- `src/` - soluzione e progetti class library
- `tests/` - progetti di test (xUnit/NUnit)
- `samples/` - esempi d'uso della libreria

## Policy operative (obbligatorie per l'agent AI)

1. Non committare token, segreti, `.env` reali o file di credenziali.
2. Sviluppo quotidiano su `dev`; verso `main` solo PR di release.
3. Non modificare ruleset, branch protection, remote o configurazione GitHub:
   sono gestiti da GitHub-Manager.
4. Non eliminare o spostare file/cartelle esistenti senza proposta motivata e conferma.
5. A ogni richiesta valuta i rischi: per operazioni che incidono pesantemente sul
   codice (refactor estesi, eliminazioni, migrazioni) chiedi conferma all'utente;
   per modifiche ordinarie procedi senza interruzioni inutili.
6. Mantieni il codice ordinato, pulito e documentato; commenti solo dove spiegano
   scelte non ovvie.
7. Changelog breve e conciso in `CHANGELOG.md` a ogni release.
8. Documenta in `Docs/` dopo aver studiato i sorgenti reali:
   non inventare path, comandi o architetture.
9. Usa `.memory/` per note di sessione e decisioni (solo `.info` e' versionato).

## Istruzioni progetto (DA COMPLETARE DALL'AGENT AI)

> L'agent AI deve compilare questa sezione al primo avvio, subito dopo il setup
> guidato da `prompt_zero.txt`, e mantenerla aggiornata nel tempo.

- Finalita del progetto: SDK .NET indipendente per la Public API Postiz.
- Stack effettivo e versioni: C#/.NET 8 e 9, xUnit, NuGet `1.0.0-alpha.1`.
- Comandi setup / avvio locale: `dotnet restore Postiz.NET.slnx --configfile NuGet.config`.
- Comandi build / test: `dotnet test Postiz.NET.slnx -c Release --no-restore` e `dotnet pack`.
- Architettura e componenti: core senza dipendenze, package DI con HttpClientFactory, package ASP.NET Core health checks.
- Note operative specifiche: compatibilita pinned a Postiz v2.23.0; nessun accesso DB o regola CRM; niente publish stabile prima dei contract gate.
