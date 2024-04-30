CREATE TABLE IF NOT EXISTS coupons (
    id SERIAL PRIMARY KEY,
    code VARCHAR(255) NOT NULL UNIQUE,
    discount_type VARCHAR(50) NOT NULL,
    discount_value DECIMAL NOT NULL,
    expiration_date DATE NOT NULL
);
INSERT INTO coupons (code, discount_type, discount_value, expiration_date) VALUES
('EXAMPLECODE', 'percentage', 10.00, '2025-12-31') ON CONFLICT DO NOTHING;