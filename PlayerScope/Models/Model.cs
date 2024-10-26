using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using System;
using Object = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Object;
using ObjectType = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType;

namespace PlayerScope.Models
{
    public readonly unsafe struct Model : IEquatable<Model>
    {
        private Model(nint address)
            => Address = address;

        public readonly nint Address;

        public static readonly Model Null = new(0);

        public DrawObject* AsDrawObject
            => (DrawObject*)Address;

        public CharacterBase* AsCharacterBase
            => (CharacterBase*)Address;

        public Weapon* AsWeapon
            => (Weapon*)Address;

        public Human* AsHuman
            => (Human*)Address;

        public static implicit operator Model(nint? pointer)
            => new(pointer ?? nint.Zero);

        public static implicit operator Model(Object* pointer)
            => new((nint)pointer);

        public static implicit operator Model(DrawObject* pointer)
            => new((nint)pointer);

        public static implicit operator Model(Human* pointer)
            => new((nint)pointer);

        public static implicit operator Model(CharacterBase* pointer)
            => new((nint)pointer);

        public static implicit operator nint(Model model)
            => model.Address;

        public bool Valid
            => Address != nint.Zero;

        public bool IsCharacterBase
            => Valid && AsDrawObject->Object.GetObjectType() == ObjectType.CharacterBase;

        public bool IsHuman
            => IsCharacterBase && AsCharacterBase->GetModelType() == CharacterBase.ModelType.Human;

        public bool IsWeapon
            => IsCharacterBase && AsCharacterBase->GetModelType() == CharacterBase.ModelType.Weapon;

        public static implicit operator bool(Model actor)
            => actor.Address != nint.Zero;

        public static bool operator true(Model actor)
            => actor.Address != nint.Zero;

        public static bool operator false(Model actor)
            => actor.Address == nint.Zero;

        public static bool operator !(Model actor)
            => actor.Address == nint.Zero;

        public bool Equals(Model other)
            => Address == other.Address;

        public override bool Equals(object? obj)
            => obj is Model other && Equals(other);

        public override int GetHashCode()
            => Address.GetHashCode();

        public static bool operator ==(Model lhs, Model rhs)
            => lhs.Address == rhs.Address;

        public static bool operator !=(Model lhs, Model rhs)
            => lhs.Address != rhs.Address;


        /// <summary> I don't know a safe way to do this but in experiments this worked.
        /// The first uint at +0x8 was set to non-zero for the mainhand and zero for the offhand. </summary>
        private static (Model Mainhand, Model Offhand) DetermineMainhand(Model first, Model second)
        {
            var discriminator1 = *(ulong*)(first.Address + 0x10);
            var discriminator2 = *(ulong*)(second.Address + 0x10);
            return discriminator1 == 0 && discriminator2 != 0 ? (second, first) : (first, second);
        }

        public override string ToString()
            => $"0x{Address:X}";
    }
}