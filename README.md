<div align="center">

<img src="docs/banner.svg" alt="FarmConnect animated banner" width="100%"/>

[![Typing SVG](https://readme-typing-svg.demolab.com?font=Fira+Code&weight=600&size=22&pause=1000&color=2D4A2B&center=true&vCenter=true&width=700&lines=Farmers+list+produce+%F0%9F%8C%BE;Buyers+order+fresh+%F0%9F%9B%92;Stock+never+oversold+%F0%9F%94%92;Fair+price+history+%F0%9F%93%88;Earnings+you+can+trust+%F0%9F%92%B0)](https://git.io/typing-svg)

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)
![EF Core](https://img.shields.io/badge/EF%20Core-8-2d4a2b?style=for-the-badge)
![Bootstrap](https://img.shields.io/badge/Bootstrap-5.3-7952B3?style=for-the-badge&logo=bootstrap&logoColor=white)
![Chart.js](https://img.shields.io/badge/Chart.js-4-FF6384?style=for-the-badge&logo=chartdotjs&logoColor=white)

</div>

---

## 🌱 What is FarmConnect (M1)?

A farmer-to-buyer agricultural marketplace. **Farmers** publish produce and track earnings, **Buyers** search and order, **Admins** monitor everything.

| # | Module | Status |
|---|--------|:------:|
| 1 | Registration, login, **Farmer / Buyer / Admin** roles (ASP.NET Core Identity) | ✅ |
| 2 | Farmer produce management (add, edit, deactivate, remove-safe, image upload) | ✅ |
| 3 | Buyer marketplace: search, grade / price / location filters, sorting, detail page | ✅ |
| 4 | Order placement: min-qty + stock validation, **atomic transaction**, stock reserve/release | ✅ |
| 5 | **Price history from SQL view** `vw_PriceHistory` + Chart.js graph | ✅ |
| 6 | Farmer earnings: daily / weekly / monthly / custom, gross, costs, net proceeds | ✅ |

## 🚀 Quick start

```bash
# prerequisites: .NET 8 SDK + SQL Server (LocalDB / Express / Docker)
cd FarmConnect
dotnet restore
dotnet run
```

Open the URL printed in the console. On first start the app **creates the database, roles, the price-history SQL view and demo data automatically.**

**Different SQL Server?** Edit `ConnectionStrings:Default` in `appsettings.json`.
Docker example:

```bash
docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Your_strong_pass1' -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
# Server=localhost,1433;Database=FarmConnectDb;User Id=sa;Password=Your_strong_pass1;TrustServerCertificate=True
```

**Want real EF migrations?** (the app uses them automatically if they exist)

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## 🔑 Demo accounts

| Role | Email | Password |
|------|-------|----------|
| 🛡️ Admin | `admin@farmconnect.com` | `Admin@12345` |
| 🧑‍🌾 Farmer | `farmer@demo.com` | `Demo@12345` |
| 🛒 Buyer | `buyer@demo.com` | `Demo@12345` |

> Change the admin credentials in `appsettings.json → Seed` before any real deployment.

## 🧭 Architecture

```mermaid
flowchart LR
  B[Browser<br/>Razor + Bootstrap + JS] --> C[ASP.NET Core MVC Controllers]
  C --> A[Identity + Role Authorization]
  C --> D[(SQL Server via EF Core)]
  D --> V[[vw_PriceHistory view]]
  V --> C
```

```mermaid
erDiagram
  AspNetUsers ||--o| FarmerProfiles : has
  AspNetUsers ||--o| BuyerProfiles : has
  AspNetUsers ||--o{ Listings : publishes
  Produce ||--o{ Listings : describes
  Produce ||--o{ PriceHistory : priced_in
  AspNetUsers ||--o{ Orders : places
  Orders ||--|{ OrderItems : contains
  Listings ||--o{ OrderItems : ordered_in
  Orders ||--o{ SellingExpenses : records
```

## 🔒 Order flow (concurrency-safe)

```mermaid
sequenceDiagram
  participant Buyer
  participant App
  participant DB
  Buyer->>App: POST quantity
  App->>App: validate active, min qty, stock
  App->>DB: BEGIN TRANSACTION
  App->>DB: UPDATE Listings SET Qty = Qty - n WHERE Qty >= n AND Active
  alt 0 rows affected
    DB-->>App: stock changed
    App->>DB: ROLLBACK
    App-->>Buyer: please retry
  else 1 row
    App->>DB: mark SoldOut if 0, INSERT Order + OrderItem (price snapshot)
    App->>DB: COMMIT
    App-->>Buyer: Order placed ✅
  end
```

**Stock rules:** reserved when an order is *Placed* → released if *Rejected/Cancelled* → counted as *sold* when *Completed*. A DB `CHECK` constraint prevents negative stock. `AgreedUnitPrice` is stored per order item, so old revenue never changes when prices change.

## 🛡️ Security highlights

- Identity password hashing, lockout after 5 failed logins, anti-forgery tokens on every POST
- Role-based `[Authorize]`; Admin can **never** be chosen at registration (seeded only)
- Ownership checks: farmers edit only their own listings; buyers see only their own orders
- Server-side validation for every rule (JS is only a convenience)
- Listings with orders are deactivated, never hard-deleted

## 📁 Structure

```
FarmConnect/
├─ Controllers/   Account · Dashboard · Listings · Market · Orders · Price · Earnings · Admin
├─ Models/        Entities.cs · ViewModels.cs
├─ Data/          ApplicationDbContext · DbSeeder (roles, SQL view, sample data)
├─ Views/         Razor pages (Greenaura-inspired UI)
├─ wwwroot/       css · js · uploads
└─ Database/      vw_PriceHistory.sql
```

## ✅ Manual test checklist

- [ ] Farmer lists **500 kg**, min **50 kg**, ₹25/kg tomatoes
- [ ] Buyer orders 20 kg → ❌ below minimum · 600 kg → ❌ insufficient stock
- [ ] Buyer orders 100 kg → ✅ stock becomes **400 kg**
- [ ] Farmer *Accepts → Marks completed* → appears in **Earnings** (₹2,500)
- [ ] Rejecting / cancelling a placed order **restores** stock
- [ ] Farmer B cannot open `/Listings/Edit/{id}` of Farmer A (404)
- [ ] Price History shows chart + table from the SQL view
- [ ] Admin can lock users and deactivate listings

## 🗺️ Notes

- Price data is a **clearly labelled sample dataset**; plug in AGMARKNET later. Never mix ₹/kg and ₹/quintal.
- *Net proceeds* = gross − recorded selling costs; it is **not** farm profit.
- Hero photo is loaded from Unsplash; swap it in `wwwroot/css/site.css` if you prefer a local image.

<div align="center"><sub>Built with purpose. Delivered with care. 🌾</sub></div>
