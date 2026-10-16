package dev.hitfix.lunar;

import java.io.File;
import java.lang.instrument.ClassFileTransformer;
import java.lang.instrument.Instrumentation;
import java.security.ProtectionDomain;
import java.util.jar.JarFile;
import org.objectweb.asm.ClassReader;
import org.objectweb.asm.ClassVisitor;
import org.objectweb.asm.ClassWriter;
import org.objectweb.asm.FieldVisitor;
import org.objectweb.asm.MethodVisitor;
import org.objectweb.asm.Opcodes;

public final class HitFixAgent {
    public static void premain(String options, Instrumentation instrumentation) throws Exception {
        File agent = new File(HitFixAgent.class.getProtectionDomain().getCodeSource().getLocation().toURI());
        instrumentation.appendToBootstrapClassLoaderSearch(new JarFile(agent));
        HitFixLog.write("Agent loaded; Java " + System.getProperty("java.version"));
        instrumentation.addTransformer(new MinecraftTransformer());
    }

    public static final class MinecraftTransformer implements ClassFileTransformer {
        public byte[] transform(ClassLoader loader, String className, Class<?> redefined,
                                ProtectionDomain domain, byte[] original) {
            if (!"net/minecraft/client/Minecraft".equals(className)) {
                return null;
            }
            try {
                ClassReader reader = new ClassReader(original);
                ClassWriter writer = new ClassWriter(reader, ClassWriter.COMPUTE_MAXS);
                final int[] matches = new int[2];
                final boolean[] alreadyHooked = new boolean[1];
                reader.accept(new ClassVisitor(Opcodes.ASM9, writer) {
                    private boolean hasInitializer;

                    public FieldVisitor visitField(int access, String name, String descriptor,
                                                   String signature, Object value) {
                        if (name.startsWith("hitfix$")) {
                            alreadyHooked[0] = true;
                        }
                        return super.visitField(access, name, descriptor, signature, value);
                    }

                    public MethodVisitor visitMethod(int access, String name, String descriptor,
                                                     String signature, String[] exceptions) {
                        MethodVisitor delegate = super.visitMethod(access, name, descriptor, signature, exceptions);
                        if (name.equals("<clinit>")) {
                            hasInitializer = true;
                            return new MethodVisitor(Opcodes.ASM9, delegate) {
                                public void visitCode() {
                                    super.visitCode();
                                    initializeHooks(this);
                                }
                            };
                        }
                        final boolean frame = name.equals("runGameLoop") && descriptor.equals("()V");
                        final boolean tick = name.equals("runTick") && descriptor.equals("()V");
                        if (!frame && !tick) {
                            return delegate;
                        }
                        matches[frame ? 0 : 1]++;
                        return new MethodVisitor(Opcodes.ASM9, delegate) {
                            public void visitCode() {
                                super.visitCode();
                                invokeHook(this, frame ? "hitfix$frameHook" : "hitfix$tickHook");
                            }

                            public void visitInsn(int opcode) {
                                if (tick && opcode == Opcodes.RETURN) {
                                    invokeHook(this, "hitfix$endHook");
                                }
                                super.visitInsn(opcode);
                            }
                        };
                    }

                    public void visitEnd() {
                        for (String field : new String[] {"hitfix$frameHook", "hitfix$tickHook", "hitfix$endHook"}) {
                            super.visitField(Opcodes.ACC_PRIVATE | Opcodes.ACC_STATIC | Opcodes.ACC_FINAL,
                                field, "Ljava/util/function/Consumer;", null, null).visitEnd();
                        }
                        if (!hasInitializer) {
                            MethodVisitor initializer = super.visitMethod(Opcodes.ACC_STATIC, "<clinit>", "()V", null, null);
                            initializer.visitCode();
                            initializeHooks(initializer);
                            initializer.visitInsn(Opcodes.RETURN);
                            initializer.visitMaxs(0, 0);
                            initializer.visitEnd();
                        }
                        super.visitEnd();
                    }
                }, 0);
                if (alreadyHooked[0]) {
                    HitFixLog.write("Minecraft already contains HitFix hooks; skipping duplicate installation");
                    return null;
                }
                if (matches[0] != 1 || matches[1] != 1) {
                    HitFixLog.write("INCOMPATIBLE: expected runGameLoop and runTick; no changes applied");
                    return null;
                }
                HitFixLog.write("Installed frame-start, tick-start and tick-end hooks in Minecraft");
                return writer.toByteArray();
            } catch (Throwable failure) {
                HitFixLog.write("TRANSFORM FAILED: " + failure);
                return null;
            }
        }

        private static void invokeHook(MethodVisitor method, String field) {
            method.visitFieldInsn(Opcodes.GETSTATIC, "net/minecraft/client/Minecraft", field, "Ljava/util/function/Consumer;");
            method.visitVarInsn(Opcodes.ALOAD, 0);
            method.visitMethodInsn(Opcodes.INVOKEINTERFACE, "java/util/function/Consumer", "accept", "(Ljava/lang/Object;)V", true);
        }

        private static void initializeHooks(MethodVisitor method) {
            method.visitMethodInsn(Opcodes.INVOKESTATIC, "java/lang/ClassLoader", "getSystemClassLoader", "()Ljava/lang/ClassLoader;", false);
            method.visitLdcInsn("dev.hitfix.lunar.RuntimeHooks");
            method.visitMethodInsn(Opcodes.INVOKEVIRTUAL, "java/lang/ClassLoader", "loadClass", "(Ljava/lang/String;)Ljava/lang/Class;", false);
            method.visitLdcInsn("createHooks");
            method.visitInsn(Opcodes.ICONST_0);
            method.visitTypeInsn(Opcodes.ANEWARRAY, "java/lang/Class");
            method.visitMethodInsn(Opcodes.INVOKEVIRTUAL, "java/lang/Class", "getMethod",
                "(Ljava/lang/String;[Ljava/lang/Class;)Ljava/lang/reflect/Method;", false);
            method.visitInsn(Opcodes.ACONST_NULL);
            method.visitInsn(Opcodes.ICONST_0);
            method.visitTypeInsn(Opcodes.ANEWARRAY, "java/lang/Object");
            method.visitMethodInsn(Opcodes.INVOKEVIRTUAL, "java/lang/reflect/Method", "invoke",
                "(Ljava/lang/Object;[Ljava/lang/Object;)Ljava/lang/Object;", false);
            method.visitTypeInsn(Opcodes.CHECKCAST, "[Ljava/util/function/Consumer;");
            method.visitInsn(Opcodes.DUP);
            method.visitInsn(Opcodes.ICONST_0);
            method.visitInsn(Opcodes.AALOAD);
            method.visitFieldInsn(Opcodes.PUTSTATIC, "net/minecraft/client/Minecraft", "hitfix$frameHook", "Ljava/util/function/Consumer;");
            method.visitInsn(Opcodes.DUP);
            method.visitInsn(Opcodes.ICONST_1);
            method.visitInsn(Opcodes.AALOAD);
            method.visitFieldInsn(Opcodes.PUTSTATIC, "net/minecraft/client/Minecraft", "hitfix$tickHook", "Ljava/util/function/Consumer;");
            method.visitInsn(Opcodes.ICONST_2);
            method.visitInsn(Opcodes.AALOAD);
            method.visitFieldInsn(Opcodes.PUTSTATIC, "net/minecraft/client/Minecraft", "hitfix$endHook", "Ljava/util/function/Consumer;");
        }
    }
}
