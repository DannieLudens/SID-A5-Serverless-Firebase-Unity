using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;

public class FirebaseManager : MonoBehaviour
{
    // ====================================================
    // SINGLETON
    // ====================================================
    // El Singleton garantiza que solo exista UN FirebaseManager
    // en toda la aplicación y que persista entre escenas
    public static FirebaseManager Instance { get; private set; }

    // ====================================================
    // REFERENCIAS A FIREBASE
    // ====================================================
    private FirebaseAuth auth;           // Para autenticación
    private DatabaseReference dbRef;     // Para la base de datos
    private FirebaseUser currentUser;    // Usuario actual

    // ====================================================
    // PROPIEDADES PÚBLICAS
    // ====================================================
    public FirebaseUser CurrentUser => currentUser;
    public bool IsAuthenticated => currentUser != null;
    public string CurrentUserId => currentUser?.UserId;

    // Datos adicionales del usuario (guardados en la DB)
    public string CurrentUsername { get; private set; }

    // ====================================================
    // INICIALIZACIÓN
    // ====================================================
    void Awake()
    {
        // Singleton: si ya existe una instancia, destruir esta
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad: persiste entre escenas
        DontDestroyOnLoad(gameObject);

        // Inicializar Firebase
        InicializarFirebase();
    }

    void InicializarFirebase()
    {
        // Verificar que todas las dependencias de Firebase estén disponibles
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            DependencyStatus dependencyStatus = task.Result;

            if (dependencyStatus == DependencyStatus.Available)
            {
                // Firebase está listo
                auth = FirebaseAuth.DefaultInstance;
                dbRef = FirebaseDatabase.DefaultInstance.RootReference;

                // Escuchar cambios en el estado de autenticación
                // Si el usuario ya tenía sesión iniciada, esto lo detecta automáticamente
                auth.StateChanged += OnAuthStateChanged;

                Debug.Log("Firebase inicializado correctamente");
            }
            else
            {
                Debug.LogError("Firebase no pudo inicializarse: " + dependencyStatus);
            }
        });
    }

    // Se llama automáticamente cuando cambia el estado de auth
    // (login, logout, sesión restaurada)
    void OnAuthStateChanged(object sender, EventArgs e)
    {
        if (auth.CurrentUser != currentUser)
        {
            currentUser = auth.CurrentUser;

            if (currentUser != null)
            {
                Debug.Log("Usuario autenticado: " + currentUser.Email);
                // Cargar el username desde la DB
                StartCoroutine(CargarUsername());
            }
            else
            {
                Debug.Log("Usuario cerró sesión");
                CurrentUsername = "";
            }
        }
    }

    // ====================================================
    // AUTENTICACIÓN
    // ====================================================

    // REGISTRO con email, password y username
    public void Registrar(string email, string password, string username,
        Action<bool, string> callback)
    {
        auth.CreateUserWithEmailAndPasswordAsync(email, password)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    // Traducir el error de Firebase a español
                    string error = TraducirError(task.Exception);
                    callback(false, error);
                    return;
                }

                // Registro exitoso — guardar el username en la DB
                FirebaseUser newUser = task.Result.User;
                currentUser = newUser;
                CurrentUsername = username;

                // Guardar datos del usuario en Realtime Database
                GuardarDatosUsuario(newUser.UserId, username, email, (success, msg) =>
                {
                    if (success)
                        callback(true, "Registro exitoso. Bienvenido " + username);
                    else
                        callback(false, "Usuario creado pero error al guardar datos: " + msg);
                });
            });
    }

    // LOGIN con email y password
    public void Login(string email, string password, Action<bool, string> callback)
    {
        auth.SignInWithEmailAndPasswordAsync(email, password)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    string error = TraducirError(task.Exception);
                    callback(false, error);
                    return;
                }

                currentUser = task.Result.User;
                callback(true, "Login exitoso");
            });
    }

    // RECUPERAR CONTRASEÑA — Firebase envía un email automáticamente
    public void RecuperarPassword(string email, Action<bool, string> callback)
    {
        auth.SendPasswordResetEmailAsync(email)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    string error = TraducirError(task.Exception);
                    callback(false, error);
                    return;
                }

                callback(true, "Correo de recuperación enviado a " + email);
            });
    }

    // LOGOUT
    public void Logout()
    {
        auth.SignOut();
        currentUser = null;
        CurrentUsername = "";
    }

    // ====================================================
    // BASE DE DATOS — USUARIOS
    // ====================================================

    // Guardar datos del usuario al registrarse
    void GuardarDatosUsuario(string userId, string username, string email,
        Action<bool, string> callback)
    {
        // Estructura en la DB:
        // usuarios/
        //   {userId}/
        //     username: "daniel"
        //     email: "daniel@gmail.com"
        //     score: 0

        Dictionary<string, object> userData = new Dictionary<string, object>
        {
            { "username", username },
            { "email", email },
            { "score", 0 }
        };

        dbRef.Child("usuarios").Child(userId).SetValueAsync(userData)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    callback(false, task.Exception.Message);
                else
                    callback(true, "Datos guardados");
            });
    }

    // Cargar el username desde la DB
    IEnumerator CargarUsername()
    {
        if (currentUser == null) yield break;

        var task = dbRef.Child("usuarios").Child(currentUser.UserId)
            .Child("username").GetValueAsync();

        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Result.Exists)
            CurrentUsername = task.Result.Value.ToString();
    }

    // ====================================================
    // BASE DE DATOS — SCORES
    // ====================================================

    // Guardar score (solo si es mayor al actual)
    public void GuardarScore(int nuevoScore, Action<bool, string> callback)
    {
        if (currentUser == null)
        {
            callback(false, "No hay usuario autenticado");
            return;
        }

        // Primero leer el score actual para comparar
        dbRef.Child("usuarios").Child(currentUser.UserId).Child("score")
            .GetValueAsync().ContinueWithOnMainThread(task =>
            {
                int scoreActual = 0;
                if (task.Result.Exists)
                    scoreActual = int.Parse(task.Result.Value.ToString());

                // Solo guardar si el nuevo score es mayor
                if (nuevoScore > scoreActual)
                {
                    dbRef.Child("usuarios").Child(currentUser.UserId)
                        .Child("score").SetValueAsync(nuevoScore)
                        .ContinueWithOnMainThread(updateTask =>
                        {
                            if (updateTask.IsFaulted)
                                callback(false, "Error al guardar score");
                            else
                                callback(true, $"¡Nuevo récord! {scoreActual} → {nuevoScore}");
                        });
                }
                else
                {
                    callback(false, $"Score {nuevoScore} no supera tu récord de {scoreActual}");
                }
            });
    }

    // Obtener tabla de puntajes (todos los usuarios ordenados)
    public void ObtenerTabla(Action<bool, List<ScoreEntry>> callback)
    {
        dbRef.Child("usuarios").GetValueAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    callback(false, null);
                    return;
                }

                List<ScoreEntry> tabla = new List<ScoreEntry>();

                foreach (DataSnapshot child in task.Result.Children)
                {
                    string username = child.Child("username").Value?.ToString() ?? "unknown";
                    int score = 0;
                    if (child.Child("score").Exists)
                        score = int.Parse(child.Child("score").Value.ToString());

                    tabla.Add(new ScoreEntry { username = username, score = score });
                }

                // Ordenar de mayor a menor
                tabla.Sort((a, b) => b.score.CompareTo(a.score));

                callback(true, tabla);
            });
    }

    // ====================================================
    // UTILIDADES
    // ====================================================

    // Traducir errores de Firebase al español
    string TraducirError(AggregateException exception)
    {
        if (exception == null) return "Error desconocido";

        string errorCode = exception.InnerException?.Message ?? "";

        if (errorCode.Contains("email-already-in-use"))
            return "Ya existe una cuenta con ese correo";
        if (errorCode.Contains("invalid-email"))
            return "El correo no tiene un formato válido";
        if (errorCode.Contains("weak-password"))
            return "La contraseña debe tener al menos 6 caracteres";
        if (errorCode.Contains("user-not-found"))
            return "No existe cuenta con ese correo";
        if (errorCode.Contains("wrong-password"))
            return "Contraseña incorrecta";
        if (errorCode.Contains("invalid-credential"))
            return "Correo o contraseña incorrectos";
        if (errorCode.Contains("network-request-failed"))
            return "Error de conexión. Verifica tu internet";
        if (errorCode.Contains("too-many-requests"))
            return "Demasiados intentos. Intenta más tarde";

        return "Error: " + errorCode;
    }

    // ELIMINAR CUENTA
    public void EliminarCuenta(Action<bool, string> callback)
    {
        if (currentUser == null)
        {
            callback(false, "No hay usuario autenticado");
            return;
        }

        string userId = currentUser.UserId;

        // Primero borrar los datos de la DB
        dbRef.Child("usuarios").Child(userId).RemoveValueAsync()
            .ContinueWithOnMainThread(dbTask =>
            {
                if (dbTask.IsFaulted)
                {
                    callback(false, "Error al borrar datos");
                    return;
                }

                // Luego borrar el usuario de Authentication
                currentUser.DeleteAsync().ContinueWithOnMainThread(authTask =>
                {
                    if (authTask.IsFaulted)
                    {
                        callback(false, "Error al eliminar cuenta: " + 
                            TraducirError(authTask.Exception));
                        return;
                    }

                    currentUser = null;
                    CurrentUsername = "";
                    callback(true, "Cuenta eliminada correctamente");
                });
            });
    }

    void OnDestroy()
    {
        if (auth != null)
            auth.StateChanged -= OnAuthStateChanged;
    }
}

// ====================================================
// CLASE AUXILIAR PARA LA TABLA DE PUNTAJES
// ====================================================
[Serializable]
public class ScoreEntry
{
    public string username;
    public int score;
}