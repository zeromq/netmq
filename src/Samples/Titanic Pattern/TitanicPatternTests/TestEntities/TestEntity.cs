using System;
using System.Text;
using TitanicCommons;

namespace TitanicProtocolTests.TestEntities
{
    internal class TestEntity : ITitanicConvert<TestEntity>
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public TestEntity ()
        {
            Id = Guid.NewGuid ();
            Name = Id.ToString ();
        }

        public byte[] ConvertToBytes ()
        {
            var enc = Encoding.UTF8;

            var idAsBytes = Id.ToByteArray ();
            var nameAsBytes = enc.GetBytes (Name);

            var length = idAsBytes.Length + nameAsBytes.Length;

            var target = new byte[length];

            Array.Copy (idAsBytes, target, idAsBytes.Length);
            Array.Copy (nameAsBytes, 0, target, idAsBytes.Length, nameAsBytes.Length);

            return target;
        }

        public TestEntity GenerateFrom (byte[] b)
        {
            const int length_of_guid = 16;

            var idAsBytes = new byte[length_of_guid];
            var nameAsBytes = new byte[b.Length - length_of_guid];
            var lengthOfName = b.Length - length_of_guid;

            Array.Copy (b, idAsBytes, length_of_guid);
            Array.Copy (b, length_of_guid, nameAsBytes, 0, lengthOfName);

            Id = new Guid (idAsBytes);
            Name = Encoding.UTF8.GetString (nameAsBytes);

            return this;
        }
    }
}
