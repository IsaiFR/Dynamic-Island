# Instalación local y empaquetado

`install-local.ps1` publica la app WinUI 3 sin MSIX en `%LOCALAPPDATA%\Programs\Dynamic Island` y crea accesos directos en Escritorio e Inicio. Incluye los recursos XAML compilados que `dotnet publish` puede omitir en una publicación sin empaquetar.

`identity/AppxManifest.xml` declara la identidad MSIX y la capacidad `userNotificationListener`. `install-identity.ps1` crea y firma un paquete de identidad local, pero exige que su certificado público ya esté en `Cert:\LocalMachine\TrustedPeople` antes de registrarlo. No añade certificados a los almacenes de confianza de forma automática. Esta confianza a nivel de equipo requiere aprobación explícita y privilegios administrativos. Si no está presente, el script se detiene antes de sustituir la instalación operativa. Después del registro, la app solicita al usuario el permiso de lectura de notificaciones de Windows.

El instalador prepara el manifiesto y el icono desde los archivos versionados en `artifacts/identity/layout/`. Ese directorio, el paquete y el certificado público exportado son salidas regenerables excluidas de Git. La clave de firma permanece en el almacén de certificados de Windows.
