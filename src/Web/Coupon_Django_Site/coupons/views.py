from django.http import HttpResponse
from django.views.decorators.csrf import csrf_exempt
import psycopg2
import os
from django.http import JsonResponse, Http404
from django.views.decorators.csrf import csrf_exempt
from django.db import connection
from django.core import serializers
from datetime import datetime
import psycopg2.extras
# from .models import Coupon
import json

def index(request):
    return HttpResponse("Hello, world. You're at the coupon index.")

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

@csrf_exempt
def apply_coupon(request):
    if request.method == 'POST':
        data = json.loads(request.body)
        coupon_code = data.get('coupon_code')
        items = data.get('items')

        if not coupon_code or not items:
            raise Http404("Missing coupon code or items.")

        for item in items:
            if 'unit_price' not in item or not isinstance(item['unit_price'], (int, float)):
                raise Http404("Item price missing or invalid.")
            if 'units' not in item or not isinstance(item['units'], int):
                raise Http404("Item units missing or invalid.")
        conn = get_db_connection()
        cursor = conn.cursor(cursor_factory=psycopg2.extras.NamedTupleCursor)
        cursor.execute("SELECT discount_type, discount_value, expiration_date FROM coupons WHERE code = %s", (coupon_code,))
        coupon = cursor.fetchone()

        print(f"Coupon: {coupon}")
        # 
        
        if not coupon:
            raise Http404("Coupon code not found.")
        
        if coupon.expiration_date < datetime.now().date():
            raise Http404("Coupon code is expired.")

        adjusted_item_prices = []
        total_discounted_price = 0
        
        for item in items:
            item_price = float(item['unit_price'])
            item_units = item['units']

            if coupon.discount_type == 'percentage':
                discount = item_price * (float(coupon.discount_value) / 100)
            else:  # discount_type == 'fixed'
                discount = float(coupon.discount_value)

            adjusted_units = item_units
            adjusted_price = max(item_price - discount, 0) 
            adjusted_item_prices.append({
                'id': item['id'],
                'name': item['name'],
                'original_unit_price': item_price,
                'adjusted_unit_price': adjusted_price,
                'original_units': item_units,
                'adjusted_units': adjusted_units
            })

            total_discounted_price += adjusted_price * adjusted_units
        
        response = {
            'final_price': total_discounted_price,
            'adjusted_items': adjusted_item_prices
        }
        
        return JsonResponse(response)

    else:
        raise Http404