using System;

namespace Netamite.Converter {
    public class MissingDataConverterException : Exception {
        
        private Type type;

        public override string Message => "Missing converter for Type " + type + ".";

        public MissingDataConverterException(Type type) {
            this.type = type;
        }

    }
}
