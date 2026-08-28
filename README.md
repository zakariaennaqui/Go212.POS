# GO212 POS

**Système de caisse Windows local pour les vendeurs au Maroc**

> Version 1.0 | Août 2026 | PFA Internship — GO212

---

## Stack Technologique

| Couche | Technologie |
|--------|-------------|
| UI | WPF + XAML + Fluent Design |
| Architecture | MVVM + CommunityToolkit.Mvvm |
| Logique métier | C# / .NET 10 LTS |
| Base de données | MySQL 8.4 LTS (local, service Windows) |
| Accès données | Dapper + MySqlConnector |
| Validation | FluentValidation |
| Authentification | BCrypt.Net (PIN hashé + sel) |
| Logs | Serilog (fichiers journaliers locaux) |
| Tests | xUnit + Moq + FluentAssertions |
| Installateur | WiX Toolset |

---

## Structure de la Solution

```
Go212.POS.sln
├── Go212.POS.Desktop        ← WPF Views, Styles XAML, ViewModels
├── Go212.POS.Domain         ← Entités, Enums, Exceptions, Interfaces (0 dépendances)
├── Go212.POS.Application    ← Cas d'utilisation (vendre, retourner, clôturer, stock)
├── Go212.POS.Infrastructure ← Dapper + MySQL, Impression, Backup, Serilog
├── Go212.POS.Tests          ← Tests xUnit unitaires + intégration
└── Go212.POS.Installer      ← WiX Toolset
```

---

## Prérequis

- Windows 10/11 64-bit
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [MySQL 8.4 LTS](https://dev.mysql.com/downloads/mysql/) installé comme service Windows
- Visual Studio 2022 (v17.x) avec workload **".NET desktop development"**

---

## Démarrage Rapide

### 1. Configurer MySQL

```sql
-- Créer la base et l'utilisateur applicatif (droits minimums)
CREATE DATABASE go212_pos CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'go212app'@'localhost' IDENTIFIED BY 'MotDePasseFort123!';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, INDEX ON go212_pos.* TO 'go212app'@'localhost';
FLUSH PRIVILEGES;

-- Exécuter les scripts dans l'ordre
SOURCE Go212.POS.Infrastructure/Data/Scripts/V1__initial_schema.sql;
SOURCE Go212.POS.Infrastructure/Data/Scripts/V2__seed_data.sql;
```

### 2. Configurer la connexion

Créer `Go212.POS.Desktop/appsettings.local.json` (jamais commité) :

```json
{
  "ConnectionStrings": {
    "Go212POS": "Server=localhost;Port=3306;Database=go212_pos;User=go212app;Password=MotDePasseFort123!;CharSet=utf8mb4;"
  }
}
```

### 3. Build et Run

```bash
cd Go212.POS.Desktop
dotnet run
```

### 4. Tests

```bash
cd Go212.POS.Tests
dotnet test
```

---

## Compte Admin par Défaut

- **Utilisateur :** `admin`
- **PIN :** `0000` (à changer impérativement au premier login)

---

## Roadmap (6 Sprints)

- [x] **Sprint 1** — Fondation : architecture, MySQL, styles WPF, Auth
- [ ] **Sprint 2** — Catalogue : produits, catégories, taxes, stock initial
- [ ] **Sprint 3** — Vente : scan, panier, paiements, ticket ESC/POS
- [ ] **Sprint 4** — Gestion : retours, dépenses, sessions, rapports
- [ ] **Sprint 5** — Robustesse : matériel, sécurité, backup, tests
- [ ] **Sprint 6** — Livraison : installateur WiX, pilote vendeur

---

## Règles de Code

- ❌ Aucune logique métier dans le code-behind WPF
- ✅ ViewModels → services couche Application uniquement  
- ✅ Toutes les requêtes Dapper avec paramètres (0 concat SQL)
- ✅ Opérations critiques dans une transaction MySQL
- ❌ Jamais de PIN, mot de passe, ou numéro de carte dans les logs
- ✅ Montants : `decimal` uniquement (jamais `float` ou `double`)
- ✅ Une vente complétée ne peut être que annulée/remboursée, jamais supprimée

---

## Identité Visuelle

- **Couleur principale :** `#00BF63` (vert GO212)
- **Langue :** Français
- **Devise :** MAD
- **Design :** Fluent Design System WPF
