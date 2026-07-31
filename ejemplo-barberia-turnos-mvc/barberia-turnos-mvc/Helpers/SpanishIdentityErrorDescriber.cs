using Microsoft.AspNetCore.Identity;

namespace barberia_turnos_mvc.Helpers
{
    public class SpanishIdentityErrorDescriber : IdentityErrorDescriber
    {
        public override IdentityError DuplicateEmail(string email)
            => new() { Code = nameof(DuplicateEmail), Description = $"El email '{email}' ya está registrado." };

        public override IdentityError DuplicateUserName(string userName)
            => new() { Code = nameof(DuplicateUserName), Description = $"El usuario '{userName}' ya existe." };

        public override IdentityError InvalidEmail(string? email)
            => new() { Code = nameof(InvalidEmail), Description = $"El email '{email}' no es válido." };

        public override IdentityError PasswordTooShort(int length)
            => new() { Code = nameof(PasswordTooShort), Description = $"La contraseña debe tener al menos {length} caracteres." };

        public override IdentityError PasswordRequiresDigit()
            => new() { Code = nameof(PasswordRequiresDigit), Description = "La contraseña debe contener al menos un número." };

        public override IdentityError PasswordRequiresLower()
            => new() { Code = nameof(PasswordRequiresLower), Description = "La contraseña debe contener al menos una minúscula." };

        public override IdentityError PasswordRequiresUpper()
            => new() { Code = nameof(PasswordRequiresUpper), Description = "La contraseña debe contener al menos una mayúscula." };

        public override IdentityError PasswordMismatch()
            => new() { Code = nameof(PasswordMismatch), Description = "La contraseña no coincide." };

        public override IdentityError InvalidToken()
            => new() { Code = nameof(InvalidToken), Description = "Token inválido." };

        public override IdentityError DefaultError()
            => new() { Code = nameof(DefaultError), Description = "Ocurrió un error inesperado." };
    }
}