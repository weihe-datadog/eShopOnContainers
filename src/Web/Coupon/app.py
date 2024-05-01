from flask import Flask, request, jsonify, abort
import psycopg2
import os
from datetime import datetime

# Initialize Flask app
app = Flask(__name__)

# Database connection parameters
# DB_NAME = "coupon_management"
# DB_USER = "coupon_manager"
# DB_HOST = os.environ.get('COUPON_DB_HOST') or "db"
# Placeholder for DB password, to be retrieved securely
# DB_PASSWORD = os.environ.get('COUPON_DB_PASSWORD')

DB_URI = os.environ.get('COUPON_DATABASE_URL')
# Establish database connection
def get_db_connection():
    print("Establishing database connection...")
    # conn = psycopg2.connect(
    #     dbname=DB_NAME,
    #     user=DB_USER,
    #     host=DB_HOST,
    #     password=DB_PASSWORD
    # )
    conn = psycopg2.connect(DB_URI)
    print("Database connection established.")
    return conn

# Endpoint to apply a coupon code to a list of shopping items
@app.route('/apply-coupon', methods=['POST'])
def apply_coupon():
    print("Received request for /apply-coupon.")
    data = request.json
    coupon_code = data.get('coupon_code')
    items = data.get('items')  # Expected to be a list of dictionaries with item id, name, and price
    print(f"Coupon code: {coupon_code}")
    print(f"Items: {items}")

    if not coupon_code or not items:
        print("Error: Missing coupon code or items.")
        abort(400, description="Missing coupon code or items.")

    # Validate items data
    for item in items:
        if 'unit_price' not in item or not isinstance(item['unit_price'], (int, float)):
            print("Error: Item price missing or invalid.")
            abort(400, description="Item price missing or invalid.")
        if 'units' not in item or not isinstance(item['units'], int):
            print("Error: Item units missing or invalid.")
            abort(400, description="Item units missing or invalid.")

    # Connect to the database
    conn = get_db_connection()
    cursor = conn.cursor()

    # Check if coupon code exists and is valid
    cursor.execute("SELECT discount_type, discount_value, expiration_date FROM coupons WHERE code = %s", (coupon_code,))
    coupon = cursor.fetchone()
    print(f"Coupon: {coupon}")

    if not coupon:
        print("Error: Coupon code not found.")
        abort(404, description="Coupon code not found.")

    discount_type, discount_value, expiration_date = coupon

    # Check if coupon is expired
    if expiration_date < datetime.now().date():
        print("Error: Coupon code is expired.")
        abort(400, description="Coupon code is expired.")

    # Apply coupon discount to each item
    adjusted_item_prices = []
    total_discounted_price = 0
    
    for item in items:
        item_price = float(item['unit_price'])
        item_units = item['units']

        if discount_type == 'percentage':
            discount = item_price * (float(discount_value) / 100)
        else:  # discount_type == 'fixed'
            discount = float(discount_value)

        adjusted_units = item_units
        adjusted_price = max(item_price - discount, 0)  # Ensure price doesn't go below 0
        adjusted_item_prices.append({
            'id': item['id'],
            'name': item['name'],
            'original_unit_price': item_price,
            'adjusted_unit_price': adjusted_price,
            'original_units': item_units,
            'adjusted_units': adjusted_units
        })

        total_discounted_price += adjusted_price * adjusted_units
    print(f"Adjusted item prices: {adjusted_item_prices}")

    # Calculate final price
    print(f"Final price: {total_discounted_price}")

    # Close database connection
    cursor.close()
    conn.close()
    print("Database connection closed.")

    # Response with adjusted item prices and final price
    response = {
        'final_price': total_discounted_price,
        'adjusted_items': adjusted_item_prices
    }
    print(f"Response: {response}")

    return jsonify(response)

if __name__ == '__main__':
    app.run(host='0.0.0.0', debug=True)
