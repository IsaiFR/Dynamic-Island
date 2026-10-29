# Clima público con Open-Meteo

La aplicación consulta las condiciones actuales de Open-Meteo al iniciar y cada 15 minutos. No necesita API key ni archivo `.env`.

La ubicación configurada es **Loma Linda, Heroica Puebla de Zaragoza, C.P. 72477**, con las coordenadas `18.99887, -98.21347`. Open-Meteo selecciona la celda meteorológica disponible más cercana mediante `cell_selection=nearest`.

La consulta obtiene:

- temperatura a 2 metros;
- sensación térmica;
- humedad relativa;
- velocidad y dirección del viento a 10 metros;
- código meteorológico WMO y zona horaria.

Si el servicio o la conexión no están disponibles, la interfaz conserva el último dato válido. En el primer arranque muestra `—` y un estado claro en lugar de inventar valores.

Open-Meteo utiliza datos de modelos meteorológicos; no representa necesariamente una estación física situada dentro de la colonia.
