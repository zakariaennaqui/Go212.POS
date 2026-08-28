-- ============================================================
-- GO212 POS — Seed Data (Demo / First Run)
-- Version: V2__seed_data.sql
-- Non-sensitive demo data only
-- ============================================================

USE go212_pos;

-- ── Vendor (one row always) ───────────────────────────────────
INSERT INTO Vendor (Name, Phone, Address, Currency, SaleNumberPrefix, SaleNumberNext)
VALUES ('Ma Boutique GO212', '+212 600 000 000', 'Casablanca, Maroc', 'MAD', 'VTE', 1);

-- ── Default Admin user ───────────────────────────────────────
-- PIN: 0000 → real BCrypt hash (verified)
INSERT INTO User (Name, Username, PinHash, Role)
VALUES ('Administrateur', 'admin', '$2a$12$vz026FFXKq5slT/AA70f9.Wd/AGFPW68o6/D6iWH4.2zJB5yUYfL2', 1);

-- ── Categories ───────────────────────────────────────────────
INSERT INTO Category (Name, Color, IconName, DisplayOrder) VALUES
    ('Boissons',      '#2563EB', 'DrinkIcon',   1),
    ('Alimentation',  '#10B981', 'FoodIcon',    2),
    ('Pâtisserie',    '#F59E0B', 'CakeIcon',    3),
    ('Épicerie',      '#8B5CF6', 'ShopIcon',    4),
    ('Autre',         '#64748B', 'OtherIcon',   5);

-- ── Products ─────────────────────────────────────────────────
INSERT INTO Product (Name, CategoryId, PriceHT, TaxRate, Barcode, Unit, StockQuantity, StockAlertThreshold) VALUES
    ('Eau minérale 0.5L',   1, 3.33,  20.00, '6111245870003', 'bouteille', 50, 10),
    ('Jus d''orange 1L',    1, 12.50, 20.00, '6111245870004', 'bouteille', 30, 5),
    ('Café express',        1, 8.00,  20.00, NULL,            'tasse',     999, 0),
    ('Pain de mie',         2, 6.67,  20.00, '6111245870010', 'paquet',    20, 5),
    ('Croissant',           3, 4.17,  20.00, NULL,            'pièce',     30, 10),
    ('Millefeuille',        3, 18.33, 20.00, NULL,            'pièce',     15, 3),
    ('Sucre 1kg',           4, 8.33,  20.00, '6111245870020', 'kg',        25, 5),
    ('Farine 1kg',          4, 7.50,  20.00, '6111245870021', 'kg',        20, 5);

-- ── Customers (demo) ────────────────────────────────────────
INSERT INTO Customer (Name, Phone, Email, IsActive) VALUES
    ('Client Comptant',    '+212 661 234 567', 'client1@example.ma',  TRUE),
    ('Boulangerie Atlas',  '+212 662 345 678', 'atlas@example.ma',   TRUE),
    ('Café Central',       '+212 663 456 789', 'central@example.ma', TRUE);

-- ── Default Settings ─────────────────────────────────────────
INSERT INTO Setting (`Key`, Value, Description) VALUES
    ('vendor.currency',         'MAD',          'Currency code'),
    ('vendor.tax_rate_default', '20',           'Default TVA rate (%)'),
    ('pos.allow_discount',      'true',         'Allow cashier to apply discounts'),
    ('pos.max_discount_pct',    '10',           'Max discount % for Cashier role'),
    ('printer.enabled',         'false',        'Thermal printer enabled'),
    ('printer.port',            'USB001',       'Printer port'),
    ('backup.auto_enabled',     'true',         'Automatic daily backup'),
    ('backup.folder',           'C:\\GO212\\Backups', 'Backup destination folder'),
    ('session.pin_max_attempts','5',            'Max PIN attempts before lock'),
    ('session.lock_minutes',    '15',           'Lock duration in minutes');
