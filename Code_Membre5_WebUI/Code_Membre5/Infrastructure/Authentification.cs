using System.Security.Cryptography;
using Pressing.DAL;
using D = Pressing.Membre5.Demo.Domain;
using M = Pressing.Models;

namespace Pressing.Membre5.Demo.Infrastructure;

// Connexion par la table utilisateur. Mot de passe : PBKDF2-SHA256, 100 000 itérations,
// 32 octets, sel Base64 (même format que les scripts SQL de l'équipe).
public static class Authentification
{
    public static M.Utilisateur Connecter(string? login, string? motDePasse)
    {
        var u = UtilisateurRepository.TrouverParLogin(login ?? "");
        if (u is null || !u.Actif || !Verifier(motDePasse ?? "", u))
            throw new D.RegleMetierException("Identifiant ou mot de passe incorrect.");
        return u;
    }

    private static bool Verifier(string motDePasse, M.Utilisateur u)
    {
        try
        {
            byte[] attendu = Convert.FromBase64String(u.MotDePasseHash);
            byte[] calcule = Rfc2898DeriveBytes.Pbkdf2(motDePasse, Convert.FromBase64String(u.Sel), 100_000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(attendu, calcule);
        }
        catch (FormatException) { return false; }
    }
}
