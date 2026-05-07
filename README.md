# SID — Actividad 5: Arquitectura Serverless con Firebase + Unity

**Estudiante:** Daniel Esteban Ardila Alzate  
**Asignatura:** Sistemas Interactivos Distribuidos
**Profesor:** Manuel Enrique Villarreal Arango 
**Proyecto Firebase:** SID-A5-Firebase-Unity

---

## ¿Qué es Serverless?

En las actividades anteriores (A3 y A4), nosotros éramos responsables del servidor:
- En A3 nos conectamos a un servidor externo (render.com)
- En A4 **nosotros mismos** construimos y corrimos el servidor en nuestra máquina

**El problema** de tener tu propio servidor es que alguien tiene que:
- Mantenerlo encendido 24/7
- Actualizarlo cuando hay vulnerabilidades
- Escalar cuando hay muchos usuarios
- Pagar la infraestructura

**Serverless** significa que delegamos todo eso a un proveedor (en este caso Google con Firebase). Nosotros solo escribimos la lógica de nuestra aplicación — Google se encarga del resto. No hay "cero servidores" en realidad, simplemente **nosotros no los administramos**.

---

## ¿Qué es Firebase?

Firebase es una plataforma de Google que ofrece múltiples servicios para aplicaciones. En esta actividad usamos dos:

### 1. Firebase Authentication
Maneja todo el sistema de usuarios:
- Registro con email y contraseña
- Login
- Recuperación de contraseña (envía el email automáticamente)
- Sesiones persistentes (recuerda al usuario aunque cierre la app)
- Cada usuario recibe un **UID único** que lo identifica en toda la plataforma

### 2. Firebase Realtime Database
Es una base de datos NoSQL que guarda datos en formato JSON y los sincroniza en tiempo real. La estructura de nuestros datos quedó así:

```json
{
  "usuarios": {
    "UID_del_usuario": {
      "username": "Daniel",
      "email": "daniel@gmail.com",
      "score": 306
    }
  }
}
```

La clave de cada usuario es su **UID de Authentication** — así conectamos los dos servicios.

---

## Arquitectura del proyecto

```
┌─────────────────┐         ┌──────────────────────┐
│  Unity (Cliente) │ ──────▶ │  Firebase Auth       │
│                 │         │  (Autenticación)      │
│  AuthScene      │ ◀────── │                      │
│  GameScene      │         └──────────────────────┘
│                 │                    │
│                 │         ┌──────────────────────┐
│                 │ ──────▶ │  Realtime Database   │
│                 │         │  (Datos y scores)    │
│                 │ ◀────── │                      │
└─────────────────┘         └──────────────────────┘
                                        │
                            ┌──────────────────────┐
                            │  Página Web (HTML)   │
                            │  GitHub Pages        │
                            │  (Tabla pública)     │
                            └──────────────────────┘
```

---

## Scripts principales

### FirebaseManager.cs

Es el script más importante. Funciona como **Singleton** — esto significa que solo existe una instancia en toda la aplicación y persiste entre escenas gracias a `DontDestroyOnLoad`.

**¿Por qué Singleton?**  
Firebase necesita inicializarse una sola vez. Si destruyéramos el FirebaseManager al cambiar de escena, tendríamos que reconectar a Firebase en cada escena. Con el Singleton, se inicializa una vez en la AuthScene y sigue disponible en la GameScene.

```csharp
void Awake()
{
    if (Instance != null && Instance != this)
    {
        Destroy(gameObject); // Si ya existe uno, destruir este duplicado
        return;
    }
    Instance = this;
    DontDestroyOnLoad(gameObject); // Persistir entre escenas
}
```

**Funcionalidades:**

| Método | Qué hace |
|--------|----------|
| `InicializarFirebase()` | Verifica dependencias y conecta Auth + DB |
| `OnAuthStateChanged()` | Se llama automáticamente cuando cambia el estado del usuario |
| `Registrar()` | Crea usuario en Auth y guarda datos en DB |
| `Login()` | Autentica con email y password |
| `RecuperarPassword()` | Envía email de recuperación (Firebase lo hace solo) |
| `Logout()` | Cierra sesión |
| `EliminarCuenta()` | Borra datos de DB y luego elimina de Auth |
| `GuardarScore()` | Compara con récord actual y guarda solo si es mayor |
| `ObtenerTabla()` | Lee todos los usuarios y los ordena por score |

**Flujo de autenticación:**

```
Usuario abre la app
        │
        ▼
Firebase revisa si hay sesión activa (OnAuthStateChanged)
        │
   ┌────┴────┐
   │         │
Hay sesión  No hay sesión
   │         │
   ▼         ▼
GameScene  AuthScene
(directo)  (pedir login)
```

**ContinueWithOnMainThread:**  
Las operaciones de Firebase son **asíncronas** — tardan un momento en ir hasta los servidores de Google y volver. `ContinueWithOnMainThread` garantiza que el código del callback se ejecute en el **hilo principal de Unity**, que es el único que puede modificar la interfaz gráfica.

```csharp
auth.SignInWithEmailAndPasswordAsync(email, password)
    .ContinueWithOnMainThread(task => {
        // Aquí ya estamos en el hilo principal y podemos tocar la UI
        textMensaje.text = "¡Bienvenido!";
    });
```

---

### AuthManager.cs

Conecta la UI de la AuthScene con el FirebaseManager. Maneja:
- Navegación entre los 3 paneles (Login, Registro, Recuperar)
- Validaciones del lado del cliente (campos vacíos, contraseña muy corta)
- Llamadas a FirebaseManager y muestra de mensajes de respuesta
- Redirección a GameScene tras login/registro exitoso

**Verificación de sesión activa:**
```csharp
IEnumerator VerificarSesion()
{
    yield return new WaitForSeconds(1.5f); // Tiempo para que Firebase se inicialice
    
    if (FirebaseManager.Instance.IsAuthenticated)
        SceneManager.LoadScene("GameScene"); // Ya hay sesión, ir directo al juego
}
```

---

### GameManager.cs

Maneja la GameScene:
- Muestra el nombre de bienvenida del usuario
- Navegación entre PanelMenu, PanelJuego y PanelTabla
- Logout y eliminación de cuenta
- Carga y muestra la tabla de puntajes
- Recibe el score del juego y lo envía a Firebase

---

### MemoryGame.cs y Carta.cs

El juego de memoria:
- 16 cartas (8 pares) distribuidas aleatoriamente con el algoritmo **Fisher-Yates**
- El jugador voltea dos cartas por turno
- Si son del mismo color = par encontrado
- Si no = se voltean de regreso
- El score se calcula así:
  - **Por par:** `max(10, 50 - intentos * 2)` — menos intentos = más puntos
  - **Bonus al terminar:** `max(0, 200 - tiempo * 2)` — más rápido = más puntos

---

## Sistema de Token vs Firebase Auth

En las actividades anteriores generábamos tokens manualmente con `Guid.NewGuid()`. Firebase Auth hace algo mucho más sofisticado:

| Actividad 4 (manual) | Firebase Auth |
|---------------------|---------------|
| Token = GUID aleatorio | Token = JWT firmado por Google |
| Se guarda en memoria (se pierde al reiniciar) | Persiste en el dispositivo |
| Sin expiración | Expira automáticamente (1 hora, se renueva solo) |
| Sin encriptación | Firmado criptográficamente |
| Solo válido en nuestro servidor | Válido en todos los servicios de Firebase |

---

## Reglas de seguridad de la Database

Firebase permite definir quién puede leer y escribir en la DB:

```json
{
  "rules": {
    ".read": "auth != null",
    ".write": "auth != null"
  }
}
```

Esto significa: solo usuarios autenticados pueden acceder. Si alguien intenta leer la DB sin token, Firebase responde con error 401.

---

## Flujo completo de la aplicación

```
1. REGISTRO
   Usuario llena: username, email, password
          │
          ▼
   Firebase Auth crea el usuario → devuelve UID
          │
          ▼
   Realtime DB guarda: { username, email, score: 0 } bajo ese UID
          │
          ▼
   App carga GameScene

2. LOGIN
   Usuario llena: email, password
          │
          ▼
   Firebase Auth valida credenciales → devuelve token JWT
          │
          ▼
   FirebaseManager carga el username desde la DB
          │
          ▼
   App carga GameScene

3. JUEGO
   Usuario juega el juego de memoria
          │
          ▼
   Al terminar: score calculado
          │
          ▼
   Firebase lee score actual del usuario
          │
   ┌──────┴──────┐
Nuevo > Actual  Nuevo menor o igual
          │           │
          ▼           ▼
   Guarda nuevo    No guarda
   score en DB     (mantiene récord)

4. TABLA DE PUNTAJES
   App lee todos los usuarios de la DB
          │
          ▼
   Ordena por score de mayor a menor
          │
          ▼
   Muestra lista con usuario actual resaltado
```

---

## Estructura de archivos

```
Assets/
├── Scripts/
│   ├── FirebaseManager.cs   ← Core: Firebase Auth + Database
│   ├── AuthManager.cs       ← UI de autenticación
│   ├── GameManager.cs       ← UI del juego y tabla
│   ├── MemoryGame.cs        ← Lógica del juego de memoria
│   └── Carta.cs             ← Componente de cada carta
├── Scenes/
│   ├── AuthScene            ← Login, Registro, Recuperar contraseña
│   └── GameScene            ← Menú, Juego, Tabla de puntajes
├── Prefabs/
│   ├── CartaPrefab          ← Botón que representa una carta
│   └── FilaScorePrefab      ← Fila de la tabla de puntajes
└── StreamingAssets/
    └── google-services.json ← Credenciales de Firebase

docs/
└── index.html               ← Página web pública con tabla de puntajes
```

---

## Página web pública

La tabla de puntajes también es accesible desde el navegador en GitHub Pages usando el **Firebase SDK para JavaScript**.

Ver tabla: `https://DannieLudens.github.io/SID-A5-Serverless-Firebase-Unity/`
