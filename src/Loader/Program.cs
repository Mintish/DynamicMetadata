using System;
using System.Reflection;
using System.Reflection.Emit;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Loader
{

    [AttributeUsage(validOn: AttributeTargets.Property | AttributeTargets.Method)]
    public class MetaInfoAttribute : Attribute {
        public MetaInfoAttribute(int publicId, string description) {
            PublicId = publicId;
            Description = description;
        }

        public int PublicId { get; set; }
        public string Description { get; set; }
    }

    class Program
    {

        private static IDictionary<string, object[]> _propMetadata = new Dictionary<string, object[]> {
            {
                "ID",
                new object[] {
                    5000000,
                    "Unique ID that identifies an individual action."
                }
            },
            {
                "Type",
                new object[] {
                    5000001,
                    "Type of the action. Foreign key to ActionType."
                }
            },
            {
                "Scheduled",  
                new object[] {
                    5000002,
                    "The scheduled date of the action."
                }
            },
            {
                "Entered",
                new object[] {
                    5000003,
                    "The entered date of the action."
                }
            },
            {
                "Comment",
                new object[] {
                    5000004,
                    "Comment about the action."
                }
            },
            {
                "HoursRemaining",
                new object[] {
                    5000005,
                    "Initial numbef of hours from date entered to scheduled date."
                }
            }
        };

        static void Main(string[] args)
        {
            var assembly = Assembly.Load("DomainObjects");
            var typeofAction = assembly.GetType("DomainObjects.Action");

            var domainObjectsName = new AssemblyName("DomainObjects.Annotated");
            var domainObjectAssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(domainObjectsName, AssemblyBuilderAccess.RunAndCollect);
            var domainObjectsModuleBuilder = domainObjectAssemblyBuilder.DefineDynamicModule($"{domainObjectsName.Name}.dll");

            var metaAttributeConstructor = typeof(MetaInfoAttribute).GetConstructors()[0];

            var actionTypeBuilder = domainObjectsModuleBuilder.DefineType(
                "DomainObjects.Annotated.Action",
                TypeAttributes.Public,
                typeofAction);

            var properties = typeofAction.GetProperties();

            foreach (var prop in properties) {

                var metaAttributeBuilder = new CustomAttributeBuilder(
                    metaAttributeConstructor, _propMetadata[prop.Name]);

                var propBuilder = actionTypeBuilder.DefineProperty(prop.Name, PropertyAttributes.None, prop.PropertyType, Type.EmptyTypes);
                propBuilder.SetCustomAttribute(metaAttributeBuilder);

                var propGetter = prop.GetGetMethod();

                if (propGetter != null) {
                    var getPassthrough = actionTypeBuilder.DefineMethod(propGetter.Name, MethodAttributes.Virtual | MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, prop.PropertyType, Type.EmptyTypes);
                    var ilGet = getPassthrough.GetILGenerator();
                    ilGet.Emit(OpCodes.Ldarg_0);
                    ilGet.EmitCall(OpCodes.Callvirt, propGetter, null);
                    ilGet.Emit(OpCodes.Ret);
                    propBuilder.SetGetMethod(getPassthrough);
                }

                var propSetter = prop.GetSetMethod();

                if (propSetter != null) {
                    var setPassthrough = actionTypeBuilder.DefineMethod(propSetter.Name, MethodAttributes.Virtual | MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, null, new [] { prop.PropertyType });
                    var ilSet = setPassthrough.GetILGenerator();
                    ilSet.Emit(OpCodes.Ldarg_0);
                    ilSet.Emit(OpCodes.Ldarg_1);
                    ilSet.EmitCall(OpCodes.Callvirt, propSetter, null);
                    ilSet.Emit(OpCodes.Ret);
                    propBuilder.SetSetMethod(setPassthrough);
                }
            }

            var typeofActionMeta = actionTypeBuilder.CreateType();

            var assemblyGenerator = new Lokad.ILPack.AssemblyGenerator();
            assemblyGenerator.GenerateAssembly(domainObjectAssemblyBuilder, "./out/DomainObjects.Annotated.dll");

            // try it out!
            DomainObjects.Action actionInstance = (DomainObjects.Action)domainObjectAssemblyBuilder.CreateInstance("DomainObjects.Annotated.Action");
            actionInstance.ID = 1234;
            actionInstance.Type = 12;
            actionInstance.Scheduled = new DateTime(2020, 02, 29);
            actionInstance.Entered = new DateTime(2020, 02, 19);
            actionInstance.Comment = "If you _see_ the llama you can't _be_ the llama.";

            var jActionInstance = JObject.FromObject(actionInstance);
            foreach (var prop in actionInstance.GetType().GetProperties()) {
                // var getMethod = prop.GetGetMethod();
                var metadata = prop.GetCustomAttribute(typeof(MetaInfoAttribute));
                if (metadata != null) {
                    jActionInstance.Add($"{prop.Name}$meta", JObject.FromObject(metadata));
                }
            }
            Console.WriteLine(jActionInstance);
        }
    }
}
