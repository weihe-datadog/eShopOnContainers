FROM registry.ddbuild.io/images/base/gbi-ubuntu_2204:release as app


USER root


RUN apt-get update && \
    apt-get upgrade -y && \
    DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends procps tini build-essential git curl libpq-dev python3-dev python3-pip && \
    apt-get -y clean && \
    rm -rf /var/lib/apt/lists/*

# Set the working directory in the container
WORKDIR /usr/src/app

# Copy the current directory contents into the container at /usr/src/app
COPY . .


ARG DD_TRACER_PY

# Install any needed packages specified in requirements.txt

RUN pip install pip --upgrade && \
    pip install wheel && \
    pip install -r requirements.txt && \
    pip install "$DD_TRACER_PY"

ENV DD_SERVICE=coupon-django-api


