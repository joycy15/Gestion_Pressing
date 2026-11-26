// =====================================================================
//  UtilisateurRepository.cs  —  Requêtes sur la table utilisateur
// ---------------------------------------------------------------------
//  Les employés : administrateur, caissier, laveur, repasseur.
//
//  Ce que fait ce fichier : lire, ajouter, modifier les employés.
//  Ce qu'il NE fait PAS : vérifier un mot de passe ou le hacher (PBKDF2).
//  Ça, c'est le travail de la BLL (Pressing.BLL/Securite). La DAL ne fait
//  que lire et écrire le hash et le sel déjà calculés.
//
//  On ne SUPPRIME jamais un employé (il est lié aux commandes et à
//  l'historique) : on le DÉSACTIVE (actif = FALSE).
// =====================================================================

using Npgsql;
using Pressing.Models;

namespace Pressing.DAL;

/// <summary>Accès à la table utilisateur.</summary>
public static class UtilisateurRepository
{
    // Colonnes lues à chaque fois (même ordre partout)
    private const string Colonnes =
        "id_utilisateur, nom, login, mot_de_passe_hash, sel, role, actif, date_creation";


    // ------------------------- Lecture -------------------------

    /// <summary>
    /// Cherche un employé par son identifiant de connexion (écran de connexion).
    /// Renvoie null si le login n'existe pas. La BLL vérifie ensuite le mot de passe et « Actif ».
    /// </summary>
    public static Utilisateur? TrouverParLogin(string login) =>
        Database.LireUn($"SELECT {Colonnes} FROM utilisateur WHERE login = @login",
            VersUtilisateur, Database.P("@login", login.Trim()));

    /// <summary>Un employé par son id, ou null.</summary>
    public static Utilisateur? ParId(int idUtilisateur) =>
        Database.LireUn($"SELECT {Colonnes} FROM utilisateur WHERE id_utilisateur = @id",
            VersUtilisateur, Database.P("@id", idUtilisateur));

    /// <summary>Tous les employés, triés par nom (écran Gestion des employés).</summary>
    public static List<Utilisateur> ListerTous() =>
        Database.LireListe($"SELECT {Colonnes} FROM utilisateur ORDER BY nom", VersUtilisateur);

    /// <summary>
    /// Vrai si ce login est déjà pris par UN AUTRE employé.
    /// idAExclure = l'employé qu'on est en train de modifier (0 pour un nouvel employé).
    /// </summary>
    public static bool LoginExiste(string login, int idAExclure = 0) =>
        (bool)Database.Scalaire(
            "SELECT EXISTS (SELECT 1 FROM utilisateur WHERE login = @login AND id_utilisateur <> @id)",
            Database.P("@login", login.Trim()), Database.P("@id", idAExclure))!;

    /// <summary>
    /// Nombre d'administrateurs actifs. La BLL s'en sert pour interdire de
    /// désactiver le DERNIER administrateur (sinon plus personne ne peut gérer l'application).
    /// </summary>
    public static int CompterAdministrateursActifs() =>
        Convert.ToInt32(Database.Scalaire(
            "SELECT COUNT(*) FROM utilisateur WHERE role = 'Administrateur' AND actif"));


    // ------------------------- Écriture -------------------------

    /// <summary>Ajoute un employé (hash et sel déjà calculés par la BLL). Renvoie son id.</summary>
    public static int Ajouter(Utilisateur u) =>
        Database.InsererEtRenvoyerId(
            "INSERT INTO utilisateur (nom, login, mot_de_passe_hash, sel, role, actif) " +
            "VALUES (@nom, @login, @hash, @sel, @role, @actif) RETURNING id_utilisateur",
            Database.P("@nom", u.Nom.Trim()),
            Database.P("@login", u.Login.Trim()),
            Database.P("@hash", u.MotDePasseHash),
            Database.P("@sel", u.Sel),
            Database.P("@role", u.Role.ToString()),     // enum -> texte ('Caissier'...)
            Database.P("@actif", u.Actif));

    /// <summary>Modifie nom, login, rôle et état actif (PAS le mot de passe). Renvoie true si trouvé.</summary>
    public static bool Modifier(Utilisateur u) =>
        Database.Executer(
            "UPDATE utilisateur SET nom = @nom, login = @login, role = @role, actif = @actif " +
            "WHERE id_utilisateur = @id",
            Database.P("@nom", u.Nom.Trim()),
            Database.P("@login", u.Login.Trim()),
            Database.P("@role", u.Role.ToString()),
            Database.P("@actif", u.Actif),
            Database.P("@id", u.IdUtilisateur)) > 0;

    /// <summary>Remplace le mot de passe (nouveau hash + nouveau sel calculés par la BLL).</summary>
    public static bool ChangerMotDePasse(int idUtilisateur, string hash, string sel) =>
        Database.Executer(
            "UPDATE utilisateur SET mot_de_passe_hash = @hash, sel = @sel WHERE id_utilisateur = @id",
            Database.P("@hash", hash),
            Database.P("@sel", sel),
            Database.P("@id", idUtilisateur)) > 0;


    // ------------------------- Ligne -> objet -------------------------

    /// <summary>Transforme une ligne de la table en objet Utilisateur.</summary>
    private static Utilisateur VersUtilisateur(NpgsqlDataReader r) => new Utilisateur
    {
        IdUtilisateur  = r.Int("id_utilisateur"),
        Nom            = r.Texte("nom"),
        Login          = r.Texte("login"),
        MotDePasseHash = r.Texte("mot_de_passe_hash"),
        Sel            = r.Texte("sel"),
        Role           = r.EnumDe<RoleUtilisateur>("role"),
        Actif          = r.Bool("actif"),
        DateCreation   = r.Date("date_creation")
    };
}
