# Arquitectura

## Proyectos

- `DynamicIsland.App`: interfaz WinUI 3, vistas, temas y composición de dependencias.
- `DynamicIsland.Core`: modelos, contratos y reglas que no dependen de Windows.
- `DynamicIsland.Infrastructure`: implementaciones para Windows y proveedores externos.

El cronómetro usa `CountdownTimer` en Core para medir tiempo transcurrido, conservar la pausa y restaurar la duración elegida tras tres segundos de finalización. La vista limita la edición a minutos y segundos. El temporizador de interfaz solo se activa mientras cuenta o muestra la finalización; cambiar de escena no detiene el conteo. Las comprobaciones con un reloj simulado están en `tests/DynamicIsland.Core.Checks`.

## Integraciones previstas

- `Media`: sesiones multimedia globales de Windows como fuente principal; Spotify Web API será un complemento opcional.
- `Notifications`: `UserNotificationListener`, siempre después del consentimiento del usuario.
- `Calendar`: adaptadores independientes para Microsoft Graph y Google Calendar.
- `Weather`: Open-Meteo mediante un proveedor HTTP sustituible, sin clave privada.
- `Clock`: reloj, temporizadores y alarmas locales.

## Reglas de rendimiento

- La isla expandida conserva un margen de permanencia de 24 píxeles lógicos alrededor de su rectángulo, incluidas las esquinas. Se contrae después de permanecer fuera durante 350 ms; el menú de posición y la animación del cronómetro suspenden ese cierre. La comprobación del cursor solo se ejecuta mientras la isla está expandida.
- Arquitectura orientada a eventos; evitar sondeo continuo.
- Mantener la ventana compacta sin foco y activar interacción solo al expandirse.
- Cargar integraciones de red de forma diferida.
- Medir antes de introducir módulos nativos en C++.

## Accesibilidad

- Todos los comandos deben ser utilizables con teclado.
- Los controles personalizados deben exponer nombre, rol, estado y valor a UI Automation.
- Respetar alto contraste, escala de texto, DPI y reducción de movimiento.
- Evitar anuncios repetitivos del progreso multimedia.
