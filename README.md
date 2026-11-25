# Marketplace API

The backend for a multi-vendor marketplace, built with **ASP.NET Core**, **Entity Framework Core** and **SQLite**. It handles accounts, role-based access, products and orders, and it is consumed by the [Marketplace App](https://github.com/anasnaeemqu/MarketPlace.App) (Flutter).

> **Live demo:** _coming soon_ &nbsp;·&nbsp; **App screenshots:** see the [Marketplace App](https://github.com/anasnaeemqu/MarketPlace.App#screenshots) repository

---

## Features

- Register and log in, with passwords stored as **BCrypt** hashes
- **JWT** authentication (tokens last 7 days)
- Three roles: **Customer**, **Vendor**, **Admin**
- Products: browse, add, edit and delete, with an optional photo link
- Orders: place, list, cancel, and move through shipping stages
- Rules are enforced on the server, so they cannot be skipped from the client

---

## Tech stack

| | |
|---|---|
| Framework | ASP.NET Core (.NET 10) |
| Data | Entity Framework Core 10, SQLite |
| Auth | JWT bearer tokens, BCrypt.Net-Next |
| Structure | `Controllers`, `Domain` (models), `Infrastructure` (database context), `Migrations` |

---

## Getting started

**You need:** the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/anasnaeemqu/MarketPlace.API.git
cd MarketPlace.API/Marketplace.API      # the inner folder that contains the .csproj
```

**1. Set a secret key for signing tokens.** It must be at least 32 characters, random, and never committed to Git:

```bash
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "<a long random string, 32+ characters>"
```

**2. Run the API:**

```bash
dotnet run --launch-profile http
```

It listens on **http://localhost:5033**. The database file (`app.db`) and its tables are created automatically on first start, and `app.db` is git-ignored.

**3. Create an admin account.** Public registration only allows Customer and Vendor, on purpose. To make an admin, register a normal account, then change its `Role` value to `Admin` directly in the database (for example with [DB Browser for SQLite](https://sqlitebrowser.org)), and log in again so the new role is in your token.

---

## Configuration

| Setting | Purpose | Default |
|---|---|---|
| `Jwt:Key` | Secret used to sign tokens. **Required.** Set it with user-secrets or an environment variable (`Jwt__Key`). | none, the app refuses to start without it |
| `Jwt:Issuer` | Token issuer | `Marketplace.API` |
| `Jwt:Audience` | Token audience | `Marketplace.Client` |
| `ConnectionStrings:DefaultConnection` | Database connection | `Data Source=app.db` |
| `Cors:Origins` | Websites allowed to call the API from a browser (array) | `["http://localhost:5000"]` |

---

## API endpoints

All routes start with `/api`. Protected routes need the header `Authorization: Bearer <token>`.

### Auth

| Method | Route | Who | Body |
|---|---|---|---|
| POST | `/Auth/register` | Anyone | `name`, `email`, `password`, `role` (`Customer` or `Vendor`) |
| POST | `/Auth/login` | Anyone | `email`, `password` |

### Products

| Method | Route | Who |
|---|---|---|
| GET | `/Products` | Anyone |
| GET | `/Products/mine` | Vendor, Admin (their own products) |
| POST | `/Products` | Vendor, Admin |
| PUT | `/Products/{id}` | The owning Vendor, or Admin |
| DELETE | `/Products/{id}` | The owning Vendor, or Admin |

### Orders

| Method | Route | Who |
|---|---|---|
| POST | `/Orders` | Customer, Admin |
| GET | `/Orders` | Logged in: your own orders (Admin sees all) |
| GET | `/Orders/{id}` | The order's owner, or Admin |
| PUT | `/Orders/{id}/status` | Admin only. Body: `{ "status": "Shipped" }` |
| PUT | `/Orders/{id}/cancel` | The owning Customer, or Admin |

---

## Business rules

- **Ownership comes from the token.** The customer id on an order and the vendor id on a product are taken from the logged-in user, never from the request body.
- **The server sets the price.** The price a client sends with an order is ignored, and the product's real price is used.
- **Stock is checked and reduced** when an order is placed. Ordering more than what is left returns a clear error.
- **Cancelling** works on Pending orders only, and puts the items back in stock.
- **Order status moves one step forward only:** Pending, then Shipped, then Delivered. Cancelled orders cannot be changed.
- **Photo links** must be `http` or `https`. Price and stock cannot be negative.

---

## Security notes

- Passwords are hashed with BCrypt and never returned in clear text.
- The signing key is not stored in the repository.
- CORS only allows the origins listed in `Cors:Origins`.
- Public registration cannot create admins.

---

## Roadmap

- [ ] Vendor "sales" endpoint: orders containing a vendor's products
- [ ] PostgreSQL for hosting, plus online deployment
- [ ] Image upload instead of photo links
- [ ] Product categories
- [ ] Automated tests
