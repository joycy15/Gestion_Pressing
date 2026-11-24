using System.Security.Cryptography;
using System.Text;

namespace Pressing.BLL.Securite;

/// <summary>
/// Hachage des mots de passe avec PBKDF2-SHA256 (inclus dans .NET, aucun package à installer).
/// En base, on stocke seulement le hash et le sel, encodés en Base64
/// (colonnes mot_de_passe_hash et sel de la table utilisateur).
///
/// NE PAS MODIFIER les constantes : les comptes de test (admin, caissier1)
/// ont été créés avec exactement ces valeurs.
/// </summary>
public static class PasswordHasher
{
	private const int TailleSel = 16;        // octets
	private const int TailleHash = 32;       // octets
	private const int Iterations = 100_000;
	private static readonly HashAlgorithmName Algorithme = HashAlgorithmName.SHA256;

	/// <summary>
	/// Crée le hash et le sel d'un nouveau mot de passe (création ou modification d'un compte).
	/// Exemple :
	/// <code>
	/// var (hash, sel) = PasswordHasher.Hacher("MonMotDePasse");
	/// // INSERT INTO utilisateur (..., mot_de_passe_hash, sel) VALUES (..., @hash, @sel)
	/// </code>
	/// </summary>
	public static (string Hash, string Sel) Hacher(string motDePasse)
	{
		byte[] sel = RandomNumberGenerator.GetBytes(TailleSel);
		byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
			Encoding.UTF8.GetBytes(motDePasse), sel, Iterations, Algorithme, TailleHash);
		return (Convert.ToBase64String(hash), Convert.ToBase64String(sel));
	}

	/// <summary>
	/// Vérifie un mot de passe saisi à la connexion.
	/// Exemple :
	/// <code>
	/// // SELECT mot_de_passe_hash, sel FROM utilisateur WHERE login = @login AND actif = TRUE
	/// bool ok = PasswordHasher.Verifier(motDePasseSaisi, hashLu, selLu);
	/// </code>
	/// </summary>
	public static bool Verifier(string motDePasse, string hashStocke, string selStocke)
	{
		try
		{
			byte[] sel = Convert.FromBase64String(selStocke);
			byte[] attendu = Convert.FromBase64String(hashStocke);
			byte[] calcule = Rfc2898DeriveBytes.Pbkdf2(
				Encoding.UTF8.GetBytes(motDePasse), sel, Iterations, Algorithme, attendu.Length);

			// Comparaison en temps constant (évite les attaques par mesure du temps)
			return CryptographicOperations.FixedTimeEquals(calcule, attendu);
		}
		catch (FormatException)
		{
			return false;   // hash ou sel mal formé en base
		}
	}
}