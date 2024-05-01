from django.urls import path

from . import views

urlpatterns = [
    path("", views.index, name="index"),
    path("apply", views.apply_coupon, name="apply_coupon"),
]