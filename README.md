# Dynamic Island para Windows 11

Aplicación de escritorio nativa construida con C#, .NET 10, WinUI 3 y Windows App SDK.

## Autor

**Enrique Isaí Figueroa Rodríguez** · [kikefigueroa5a@gmail.com](mailto:kikefigueroa5a@gmail.com)

## Requisitos

- Windows 11 x64 o ARM64.
- .NET SDK 10.0.401 o una revisión compatible.
- Windows SDK 10.0.26100 o posterior.

## Compilar

```powershell
dotnet restore DynamicIsland.slnx
dotnet build DynamicIsland.slnx -c Debug -p:Platform=x64
dotnet run --project src/DynamicIsland.App/DynamicIsland.App.csproj -p:Platform=x64
```

La isla aparece centrada en la parte superior. Coloca el cursor sobre ella para expandirla y retíralo para contraerla; el clic principal ya no cambia su tamaño. La escena Hora y Clima abre primero un calendario mensual con el día actual resaltado; el botón de nube alterna entre calendario y detalles del clima. El botón del extremo derecho recorre **Hora y Clima → Codex → Música → Cronómetro**. La escena de música utiliza las sesiones multimedia de Windows, por lo que puede mostrar y controlar Spotify, YouTube Music en un navegador compatible y otras aplicaciones que publiquen controles multimedia del sistema. La isla contraída muestra una miniatura de la carátula y controles de pista anterior, reproducir/pausar y pista siguiente. La vista ampliada muestra la carátula, el tiempo transcurrido a la izquierda de la barra de progreso y la duración total a la derecha. No necesita la API ni credenciales de Spotify: la carátula aparece cuando la sesión multimedia la proporciona. Si ninguna aplicación publica una sesión, se muestra «Sin reproducción» y un icono musical sustituye la carátula.

La secuencia de escenas incluye también **Música → Cronómetro → Hora y Clima**. El cronómetro es una cuenta regresiva de **minutos y segundos**, entre `00:01` y `99:59`, con cinco minutos como duración inicial. La vista contraída muestra el tiempo y un botón de iniciar/pausar; entra directamente sobre ese botón para usarlo sin expandir, o coloca el puntero sobre el tiempo para abrir los controles. En la vista ampliada puedes ajustar cada campo con `+` y `−`, iniciar, pausar, continuar y reiniciar. Para cambiar la duración de una cuenta iniciada, reiníciala primero.

Al llegar a cero aparece «¡Tiempo terminado!» con un icono animado durante tres segundos, incluso si estás en otra escena. Después vuelve la escena seleccionada y el cronómetro queda preparado con la duración elegida. Respeta la opción de Windows de desactivar animaciones y no reproduce sonido. El conteo continúa al cambiar de escena y utiliza tiempo transcurrido para evitar acumular retrasos entre actualizaciones; cerrar la aplicación descarta el conteo.

Cuando la isla está contraída, un cambio de texto en el portapapeles muestra durante cuatro segundos una vista previa temporal sobre cualquier ventana normal. La vista previa se mantiene solo en memoria, se recorta a 140 caracteres y luego vuelve al escenario anterior. No lee imágenes ni conserva un historial.

En la vista extendida, el botón de tres puntos de abajo a la derecha abre las posiciones Izquierda, Centro y Derecha. La isla se centra en el tercio elegido del área de trabajo superior, mantiene esa posición al contraerse o cambiar de escena y la recuerda al reiniciar.

La isla funciona como una ventana flotante de fondo: no aparece en Alt+Tab ni en la barra de tareas y se muestra sin activar la ventana. Al expandirla, cambiar de escena o pulsar sus controles, conserva el foco del teclado en la aplicación en la que estás trabajando. Sigue siendo visible sobre las ventanas normales y no registra atajos globales de teclado. Esto no configura el inicio automático con Windows.

La instalación local de este equipo usa una identidad MSIX registrada. El lector oficial de notificaciones requiere además que el usuario conceda el permiso `userNotificationListener` de Windows. Muestra durante cuatro segundos los avisos nuevos; para WhatsApp añade un contador de avisos observados del mismo chat, no un número verificado de mensajes sin leer. La API oficial expone el icono de la aplicación, pero no garantiza la foto del contacto.

El calendario muestra las fechas locales del mes, como el tablero del mod de Windhawk. Todavía no sincroniza eventos de Google Calendar ni de otras cuentas.

La escena de Codex lee los registros locales de sesión para mostrar el estado y los porcentajes restantes de 5 horas y semanal. No es una conexión oficial en tiempo real: puede haber retraso o faltar el estado de aprobación según el registro disponible. La escena del clima usa Open-Meteo sin API key para Loma Linda, Puebla; la configuración está descrita en [docs/weather.md](docs/weather.md).

## Integración del mod de Windhawk

Tomamos como referencia el mod **Dynamic Island for Windows** de Himanshu (`dynamic-island-for-windows`, MIT). Conservamos esta aplicación independiente en C#/WinUI 3: no requiere Windhawk ni inyecta código en otros procesos. Se incorporaron la idea de cápsula siempre visible, los estados compacta/expandida y la sesión multimedia del sistema con controles de reproducción. La vista de clima y la de Codex siguen siendo parte de este proyecto.

El mod también incluye actividades de batería, volumen, descargas y dispositivos. Esas actividades **no están integradas todavía**. La actividad de texto copiado al portapapeles sí está integrada; la de notificaciones necesita el permiso de Windows. La notificación de Codex y los límites dependen de los registros locales, no de una API pública de Codex.

La ventana usa un procedimiento nativo para que el área cliente ocupe toda la cápsula. Esto elimina la línea blanca del marco que WinUI 3 dibujaba en los bordes superior e inferior, sin perder el redondeo ni los clics.

La app conserva el despliegue independiente para facilitar el desarrollo. `packaging/identity` contiene el paquete de identidad MSIX para habilitar capacidades de Windows sin cambiar la ubicación de la app.

## Instalación local

Para publicar una versión Release independiente del directorio de desarrollo y crear accesos directos en Escritorio e Inicio:

```powershell
.\packaging\install-local.ps1 -Platform x64
```

La app se instala en `%LOCALAPPDATA%\Programs\Dynamic Island`. El icono del ejecutable y de los accesos se genera a partir de `tools/generate_icon.py`. Puedes volver a ejecutar el instalador para actualizar la app.

Consulta [docs/architecture.md](docs/architecture.md) para conocer la organización del código.

Las comprobaciones deterministas del contador (pausa, continuación, límites, retrasos, reinicio y finalización de tres segundos) se ejecutan con `dotnet run --project tests/DynamicIsland.Core.Checks`.

Para conocer el proveedor y la ubicación del clima, consulta [docs/weather.md](docs/weather.md).

## Archivos locales y credenciales

La aplicación no necesita un archivo `.env` ni una clave de Google Maps. Los archivos `.env`, las claves privadas, los certificados de firma privados, los registros, las cachés y las salidas de compilación están excluidos de Git. Los certificados locales para la identidad MSIX se administran en el almacén de certificados de Windows; los paquetes y certificados públicos generados quedan en `artifacts/`.

Para trabajar desde un clon limpio, ejecuta los comandos de restauración y compilación anteriores. NuGet restaura las dependencias automáticamente. El generador opcional de iconos requiere Python y Pillow; los iconos ya están incluidos en `src/DynamicIsland.App/Assets/`.
