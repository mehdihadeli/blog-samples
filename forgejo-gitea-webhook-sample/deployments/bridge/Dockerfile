FROM python:3.13-alpine

WORKDIR /app
COPY bridge.py .

ENV PORT=8080
EXPOSE 8080

CMD ["python", "bridge.py"]
