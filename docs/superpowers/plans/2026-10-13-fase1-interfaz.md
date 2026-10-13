# Fase 1 — Interfaz nueva (AncoRA Paseo 1.2.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reemplazar la interfaz IMGUI y el texto 3D del paseo por una interfaz UI Toolkit estilo iOS: bienvenida con
permisos, píldora de estado, tarjeta inferior (pista o edificio reconocido), nombres flotantes sobre las cajas y una hoja
del equipo con ajuste de caja en ejes de la caja.

**Architecture:** Las decisiones viven en clases puras con pruebas EditMode (`PaseoBoxNudge`, `PaseoStartFlow`,
`PaseoPermissionFlow`, `PaseoCard`, `PaseoCardChoice`, `PaseoTapGesture`, `PaseoMapRow`). Las vistas son clases C# que
arman `VisualElement`s en código (sin UXML, para poder probarlas sin panel) y solo dibujan. `PaseoUi` (MonoBehaviour con
`UIDocument`) lee el estado de `PaseoTour` y alimenta las vistas. `PaseoSetup.Prepare` arma la interfaz en la escena y deja
la `ARSession` apagada hasta que la bienvenida resuelve los permisos.

**Tech Stack:** Unity 6000.6.0f1, UI Toolkit (módulo `uielements`), AR Foundation 6.5, Immersal 2.4.0, NUnit EditMode,
fuente Inter (copiada de la instalación de Unity, licencia OFL).

**Spec:** `docs/superpowers/specs/2026-10-13-interfaz-guia-diagnostico-design.md` (este plan cubre §2, §3 y la parte de
interfaz de §6 y §7; la guía con GPS es la fase 2 y el registro de intentos la fase 3).

## Global Constraints

- Unity 6000.6.0f1; solo Android (IL2CPP, ARM64); el Editor cerrado para todo comando batch; nunca dos Unity a la vez.
- Textos en pantalla y logs en español; comentarios de código en inglés.
- Comando de pruebas: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests` (con
  `-Filter AncorRA.Tests.<Clase>` para una clase). Métodos: `-Mode method -Method AncorRA.Editor.PaseoSetup.<Metodo>`.
- `edificio.json` manda; `PaseoSetup.Prepare` rearma la escena desde cero; `Validate` exige lo que `Prepare` arma.
- Toda la interfaz va dentro del área segura (`GuiSafeArea.Rect`), salvo los nombres flotantes, que siguen a las cajas.
- Números en la interfaz con coma decimal, sin depender del idioma del teléfono.
- Al terminar no queda ningún componente IMGUI (`OnGUI`) ni `TextMesh` en el paseo.
- Antes de cada `adb`, `adb devices`; nunca abrir la app por adb; pedir permiso antes de desinstalar.
- Commits locales con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; push solo si el usuario lo pide.

## Review Focus

1. Permiso de cámara negado (o negado para siempre, sin diálogo) → la bienvenida lo explica y no se queda esperando; ubicación negada → el paseo arranca igual. *(Task 2)*
2. La bienvenida abierta más de 15 s → al tocar «Comenzar» no aparece «El SDK no quedó listo» por el tiempo pasado en la bienvenida. *(Task 5)*
3. Dos edificios a la vista, o una caja detrás de la cámara → la tarjeta muestra el más centrado y nunca uno que no se ve. *(Task 3)*
4. Panel del equipo sin edificio ubicado → los botones de ajuste no hacen nada y el panel lo dice. *(Task 7)*
5. Tocar «Comenzar» varias veces mientras está abierto el diálogo de permisos → un solo pedido. *(Task 2)*

---

### Task 1: Ajuste de caja en ejes de la caja

**Files:**
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoBoxNudge.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoBoxNudgeTests.cs`

**Interfaces:**
- Produces: `public static Vector3 PaseoBoxNudge.Move(Vector3 localPosition, float yaw, float right, float closer, float up)` — posición nueva en el marco del mapa; `right`/`closer`/`up` en metros, vistos por alguien parado frente a la cara con la X.

- [ ] **Step 1: Write the failing test**

```csharp
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoBoxNudgeTests
    {
        static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, "z");
        }

        [Test]
        public void RightIsTheViewersRightWhichIsTheBoxsMinusX()
        {
            AssertNear(new Vector3(-1f, 0f, 0f), PaseoBoxNudge.Move(Vector3.zero, 0f, right: 1f, closer: 0f, up: 0f));
        }

        [Test]
        public void CloserMovesTowardsTheViewerAlongTheFront()
        {
            AssertNear(new Vector3(0f, 0f, 1f), PaseoBoxNudge.Move(Vector3.zero, 0f, right: 0f, closer: 1f, up: 0f));
        }

        [Test]
        public void UpIsUpWhateverTheTurn()
        {
            AssertNear(new Vector3(2f, 1.5f, 3f), PaseoBoxNudge.Move(new Vector3(2f, 1f, 3f), 37f, right: 0f, closer: 0f, up: 0.5f));
        }

        [Test]
        public void TheMoveTurnsWithTheBox()
        {
            // Turned 90°, the front looks along map +X, so the viewer's right is map +Z.
            AssertNear(new Vector3(0f, 0f, 1f), PaseoBoxNudge.Move(Vector3.zero, 90f, right: 1f, closer: 0f, up: 0f));
            AssertNear(new Vector3(1f, 0f, 0f), PaseoBoxNudge.Move(Vector3.zero, 90f, right: 0f, closer: 1f, up: 0f));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoBoxNudgeTests`
Expected: falla la compilación (`PaseoBoxNudge` no existe). Para ver una falla de prueba real, crear antes el archivo
con `=> localPosition;` y confirmar 4 fallas.

- [ ] **Step 3: Write minimal implementation**

```csharp
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Moves a box along its own axes as seen by someone standing in front of it (facing the face with the X). The box's
    /// +Z looks at that person, so their right is the box's -X. The map frame's axes point anywhere on site, which made the
    /// old X/Y/Z steppers a guessing game.
    /// </summary>
    public static class PaseoBoxNudge
    {
        public static Vector3 Move(Vector3 localPosition, float yaw, float right, float closer, float up) =>
            localPosition + Quaternion.Euler(0f, yaw, 0f) * new Vector3(-right, up, closer);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoBoxNudgeTests`
Expected: 4 pasaron.

- [ ] **Step 5: Commit**

```bash
git add Assets/AncoRA/Scripts/Paseo/PaseoBoxNudge.cs* Assets/AncoRA/Editor/Tests/PaseoBoxNudgeTests.cs*
git commit -m "Mover la caja en sus propios ejes desde el panel del equipo" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Arranque — botón «Comenzar» y permisos

**Files:**
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoStartFlow.cs`
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoPermissionFlow.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoStartFlowTests.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoPermissionFlowTests.cs`

**Interfaces:**
- Produces: `enum PaseoStartState { Welcome, Requesting, Started, NeedsCamera }`; `PaseoStartFlow` con `State`, `LocationGranted`, `bool Press()`, `void Answer(bool camera, bool location)`.
- Produces: `interface IPaseoPermissionSystem { bool Has(string permission); void Request(string permission); }`; `PaseoPermissionFlow(IPaseoPermissionSystem)` con `Camera`, `Location`, `NoDialogSeconds`, `Done`, `CameraGranted`, `LocationGranted`, `void Begin(float now)`, `void Update(float now, bool focused)`.

- [ ] **Step 1: Write the failing tests**

`Assets/AncoRA/Editor/Tests/PaseoStartFlowTests.cs`:

```csharp
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoStartFlowTests
    {
        [Test]
        public void PressingStartWhileTheDialogsAreOpenAsksOnce()
        {
            var flow = new PaseoStartFlow();
            Assert.IsTrue(flow.Press());
            Assert.AreEqual(PaseoStartState.Requesting, flow.State);
            Assert.IsFalse(flow.Press());
        }

        [Test]
        public void WithTheCameraTheTourStartsEvenWithoutLocation()
        {
            var flow = new PaseoStartFlow();
            flow.Press();
            flow.Answer(camera: true, location: false);
            Assert.AreEqual(PaseoStartState.Started, flow.State);
            Assert.IsFalse(flow.LocationGranted);
            Assert.IsFalse(flow.Press(), "already started: the button only closes the welcome");
        }

        [Test]
        public void WithoutTheCameraTheWelcomeStaysAndCanAskAgain()
        {
            var flow = new PaseoStartFlow();
            flow.Press();
            flow.Answer(camera: false, location: true);
            Assert.AreEqual(PaseoStartState.NeedsCamera, flow.State);
            Assert.IsTrue(flow.Press());
        }

        [Test]
        public void AnAnswerNobodyAskedForIsIgnored()
        {
            var flow = new PaseoStartFlow();
            flow.Answer(camera: true, location: true);
            Assert.AreEqual(PaseoStartState.Welcome, flow.State);
        }
    }
}
```

`Assets/AncoRA/Editor/Tests/PaseoPermissionFlowTests.cs`:

```csharp
using System.Collections.Generic;
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoPermissionFlowTests
    {
        sealed class FakeSystem : IPaseoPermissionSystem
        {
            public readonly HashSet<string> Granted = new();
            public readonly List<string> Requested = new();
            public bool Has(string permission) => Granted.Contains(permission);
            public void Request(string permission) => Requested.Add(permission);
        }

        [Test]
        public void AlreadyGrantedPermissionsFinishAtOnce()
        {
            var system = new FakeSystem { Granted = { PaseoPermissionFlow.Camera, PaseoPermissionFlow.Location } };
            var flow = new PaseoPermissionFlow(system);
            flow.Begin(0f);
            Assert.IsTrue(flow.Done);
            Assert.IsTrue(flow.CameraGranted && flow.LocationGranted);
            Assert.IsEmpty(system.Requested);
        }

        [Test]
        public void CameraThenLocationOneDialogAtATime()
        {
            var system = new FakeSystem();
            var flow = new PaseoPermissionFlow(system);
            flow.Begin(0f);
            CollectionAssert.AreEqual(new[] { PaseoPermissionFlow.Camera }, system.Requested);
            flow.Update(0.2f, focused: false); // the dialog took the focus
            system.Granted.Add(PaseoPermissionFlow.Camera);
            flow.Update(0.4f, focused: true);
            CollectionAssert.AreEqual(new[] { PaseoPermissionFlow.Camera, PaseoPermissionFlow.Location }, system.Requested);
            Assert.IsFalse(flow.Done);
            flow.Update(0.6f, focused: false);
            system.Granted.Add(PaseoPermissionFlow.Location);
            flow.Update(0.8f, focused: true);
            Assert.IsTrue(flow.Done);
            Assert.IsTrue(flow.CameraGranted && flow.LocationGranted);
        }

        [Test]
        public void WithoutTheCameraTheLocationIsNotAsked()
        {
            var system = new FakeSystem();
            var flow = new PaseoPermissionFlow(system);
            flow.Begin(0f);
            flow.Update(0.2f, focused: false);
            flow.Update(0.4f, focused: true); // denied
            Assert.IsTrue(flow.Done);
            Assert.IsFalse(flow.CameraGranted);
            CollectionAssert.AreEqual(new[] { PaseoPermissionFlow.Camera }, system.Requested);
        }

        [Test]
        public void ADeniedLocationStillFinishes()
        {
            var system = new FakeSystem { Granted = { PaseoPermissionFlow.Camera } };
            var flow = new PaseoPermissionFlow(system);
            flow.Begin(0f);
            flow.Update(0.2f, focused: false);
            flow.Update(0.4f, focused: true);
            Assert.IsTrue(flow.Done);
            Assert.IsTrue(flow.CameraGranted);
            Assert.IsFalse(flow.LocationGranted);
        }

        [Test]
        public void ARequestThatShowsNoDialogDoesNotHang()
        {
            // Android shows nothing for a permission denied for good: the focus never leaves.
            var system = new FakeSystem();
            var flow = new PaseoPermissionFlow(system);
            flow.Begin(0f);
            flow.Update(PaseoPermissionFlow.NoDialogSeconds * 0.5f, focused: true);
            Assert.IsFalse(flow.Done);
            flow.Update(PaseoPermissionFlow.NoDialogSeconds, focused: true);
            Assert.IsTrue(flow.Done);
            Assert.IsFalse(flow.CameraGranted);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter "AncorRA.Tests.PaseoStartFlowTests|AncorRA.Tests.PaseoPermissionFlowTests"`
Expected: no compila (tipos faltantes). Crear las clases con métodos vacíos y `Done => false` y confirmar que fallan
por comportamiento.

- [ ] **Step 3: Write minimal implementation**

`Assets/AncoRA/Scripts/Paseo/PaseoStartFlow.cs`:

```csharp
namespace AncorRA.AR
{
    public enum PaseoStartState
    {
        Welcome,
        Requesting,
        Started,
        NeedsCamera
    }

    /// <summary>
    /// The welcome button asks for the camera and the location, and only then AR starts. Without the camera nothing
    /// works, so the welcome stays and explains; without the location the tour starts anyway (no arrows, no distances).
    /// </summary>
    public sealed class PaseoStartFlow
    {
        public PaseoStartState State { get; private set; } = PaseoStartState.Welcome;
        public bool LocationGranted { get; private set; }

        /// <summary>The button was pressed: true when the permissions must be asked now.</summary>
        public bool Press()
        {
            if (State is PaseoStartState.Requesting or PaseoStartState.Started)
                return false;
            State = PaseoStartState.Requesting;
            return true;
        }

        public void Answer(bool camera, bool location)
        {
            if (State != PaseoStartState.Requesting)
                return;
            State = camera ? PaseoStartState.Started : PaseoStartState.NeedsCamera;
            LocationGranted = location;
        }
    }
}
```

`Assets/AncoRA/Scripts/Paseo/PaseoPermissionFlow.cs`:

```csharp
namespace AncorRA.AR
{
    /// <summary>Platform side of the permission requests (Android, or always granted in the Editor).</summary>
    public interface IPaseoPermissionSystem
    {
        bool Has(string permission);
        void Request(string permission);
    }

    /// <summary>
    /// Asks for the camera, then the location, one system dialog at a time. The answer is read back with Has once the app
    /// gets its focus back (the dialog closed), all on Unity's main thread: Android's own callbacks arrive on its UI
    /// thread. A request that shows no dialog (a permission denied for good) is checked again after a short wait instead
    /// of hanging. Without the camera the flow stops: the location is not even asked.
    /// </summary>
    public sealed class PaseoPermissionFlow
    {
        public const string Camera = "android.permission.CAMERA";
        public const string Location = "android.permission.ACCESS_FINE_LOCATION";
        // Longer than a system dialog takes to appear and take the focus.
        public const float NoDialogSeconds = 1f;

        readonly IPaseoPermissionSystem system;
        int stage; // 0 camera, 1 location, 2 done
        bool waiting;
        bool sawDialog;
        float askedAt;

        public PaseoPermissionFlow(IPaseoPermissionSystem permissionSystem) => system = permissionSystem;

        public bool Done { get; private set; }
        public bool CameraGranted { get; private set; }
        public bool LocationGranted { get; private set; }

        public void Begin(float now)
        {
            stage = 0;
            waiting = false;
            Done = false;
            CameraGranted = LocationGranted = false;
            Advance(now, ask: true);
        }

        /// <summary>Call every frame while the flow runs.</summary>
        public void Update(float now, bool focused)
        {
            if (!waiting)
                return;
            if (!focused)
            {
                sawDialog = true;
                return;
            }
            if (!sawDialog && now - askedAt < NoDialogSeconds)
                return;
            waiting = false;
            Advance(now, ask: false);
        }

        void Advance(float now, bool ask)
        {
            while (stage < 2)
            {
                string permission = stage == 0 ? Camera : Location;
                bool granted = system.Has(permission);
                if (!granted && ask)
                {
                    system.Request(permission);
                    waiting = true;
                    sawDialog = false;
                    askedAt = now;
                    return;
                }
                if (stage == 0)
                    CameraGranted = granted;
                else
                    LocationGranted = granted;
                stage = stage == 0 && !granted ? 2 : stage + 1;
                ask = true;
            }
            Done = true;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter "AncorRA.Tests.PaseoStartFlowTests|AncorRA.Tests.PaseoPermissionFlowTests"`
Expected: 9 pasaron.

- [ ] **Step 5: Commit**

```bash
git add Assets/AncoRA/Scripts/Paseo/PaseoStartFlow.cs* Assets/AncoRA/Scripts/Paseo/PaseoPermissionFlow.cs* Assets/AncoRA/Editor/Tests/PaseoStartFlowTests.cs* Assets/AncoRA/Editor/Tests/PaseoPermissionFlowTests.cs*
git commit -m "Pedir cámara y ubicación desde la bienvenida, de a un permiso" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Tarjeta, edificio elegido y gesto del equipo

**Files:**
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoCard.cs`
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoCardChoice.cs`
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoTapGesture.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoCardTests.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoCardChoiceTests.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoTapGestureTests.cs`

**Interfaces:**
- Produces: `enum PaseoCardKind { None, Hint, Building }`; `readonly struct PaseoCard : IEquatable<PaseoCard>` con `Kind`, `Title`, `Subtitle`, `None`, `Hint(string, string)`, `Building(string name, string description)`, `Choose(bool sdkReady, bool tracking, string shownBuilding, string description)`, `HintTitle`, `HintSubtitle`.
- Produces: `static int PaseoCardChoice.Pick(IReadOnlyList<Vector3> viewportPoints)` (−1 si ninguno), `PaseoCardChoice.Margin`.
- Produces: `PaseoTapGesture` con `Taps`, `WindowSeconds`, `bool Tap(float now)`.

- [ ] **Step 1: Write the failing tests**

`PaseoCardTests.cs`:

```csharp
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoCardTests
    {
        [Test]
        public void AShownBuildingGetsItsCard()
        {
            var card = PaseoCard.Choose(sdkReady: true, tracking: true, "Teología", "Facultad de Teología.");
            Assert.AreEqual(PaseoCardKind.Building, card.Kind);
            Assert.AreEqual("Teología", card.Title);
            Assert.AreEqual("Facultad de Teología.", card.Subtitle);
        }

        [Test]
        public void ABuildingWithoutDescriptionHasAnEmptySubtitle()
        {
            Assert.AreEqual("", PaseoCard.Choose(true, true, "X1", null).Subtitle);
        }

        [Test]
        public void WhileSearchingTheCardHintsWhatToDo()
        {
            var card = PaseoCard.Choose(sdkReady: true, tracking: true, null, null);
            Assert.AreEqual(PaseoCardKind.Hint, card.Kind);
            Assert.AreEqual(PaseoCard.HintTitle, card.Title);
        }

        [Test]
        public void BeforeTheSdkIsReadyOrWithoutTrackingThereIsNoCard()
        {
            Assert.AreEqual(PaseoCardKind.None, PaseoCard.Choose(sdkReady: false, tracking: true, null, null).Kind);
            Assert.AreEqual(PaseoCardKind.None, PaseoCard.Choose(sdkReady: true, tracking: false, null, null).Kind);
        }

        [Test]
        public void CardsWithTheSameContentAreEqual()
        {
            Assert.AreEqual(PaseoCard.Building("X1", "a"), PaseoCard.Building("X1", "a"));
            Assert.AreNotEqual(PaseoCard.Building("X1", "a"), PaseoCard.Building("EIC", "a"));
            Assert.AreEqual(PaseoCard.None, default(PaseoCard));
        }
    }
}
```

`PaseoCardChoiceTests.cs`:

```csharp
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoCardChoiceTests
    {
        [Test]
        public void TheBoxNearestTheMiddleWins()
        {
            Assert.AreEqual(1, PaseoCardChoice.Pick(new[] { new Vector3(0.1f, 0.5f, 10f), new Vector3(0.55f, 0.45f, 20f) }));
        }

        [Test]
        public void ABoxBehindTheCameraIsNeverChosen()
        {
            // WorldToViewportPoint mirrors points behind the camera into the screen; z < 0 gives them away.
            Assert.AreEqual(1, PaseoCardChoice.Pick(new[] { new Vector3(0.5f, 0.5f, -5f), new Vector3(0.9f, 0.9f, 5f) }));
        }

        [Test]
        public void ABoxFarOutsideTheScreenIsNotChosen()
        {
            Assert.AreEqual(-1, PaseoCardChoice.Pick(new[] { new Vector3(1f + PaseoCardChoice.Margin + 0.1f, 0.5f, 5f) }));
        }

        [Test]
        public void ABoxWhoseCentreIsJustOffTheEdgeStillCounts()
        {
            // Up close, a building fills the view while its centre sits a little off screen.
            Assert.AreEqual(0, PaseoCardChoice.Pick(new[] { new Vector3(1.1f, 0.5f, 5f) }));
        }

        [Test]
        public void NothingShownMeansNoChoice()
        {
            Assert.AreEqual(-1, PaseoCardChoice.Pick(new Vector3[0]));
        }
    }
}
```

`PaseoTapGestureTests.cs`:

```csharp
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoTapGestureTests
    {
        [Test]
        public void FiveQuickTapsToggle()
        {
            var gesture = new PaseoTapGesture();
            for (int i = 0; i < 4; i++)
                Assert.IsFalse(gesture.Tap(i * 0.3f));
            Assert.IsTrue(gesture.Tap(1.2f));
        }

        [Test]
        public void ASlowTapStartsTheSeriesAgain()
        {
            var gesture = new PaseoTapGesture();
            gesture.Tap(0f);
            gesture.Tap(0.3f);
            gesture.Tap(0.6f);
            Assert.IsFalse(gesture.Tap(0.6f + PaseoTapGesture.WindowSeconds + 0.1f));
            Assert.IsFalse(gesture.Tap(2.5f));
            Assert.IsFalse(gesture.Tap(2.7f));
            Assert.IsFalse(gesture.Tap(2.9f));
            Assert.IsTrue(gesture.Tap(3.1f));
        }

        [Test]
        public void AfterAToggleItTakesFiveMoreTaps()
        {
            var gesture = new PaseoTapGesture();
            for (int i = 0; i < 5; i++)
                gesture.Tap(i * 0.2f);
            for (int i = 5; i < 9; i++)
                Assert.IsFalse(gesture.Tap(i * 0.2f));
            Assert.IsTrue(gesture.Tap(1.8f));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter "AncorRA.Tests.PaseoCardTests|AncorRA.Tests.PaseoCardChoiceTests|AncorRA.Tests.PaseoTapGestureTests"`
Expected: no compila; con esqueletos (`Choose => None`, `Pick => -1`, `Tap => false`) fallan por comportamiento.

- [ ] **Step 3: Write minimal implementation**

`PaseoCard.cs`:

```csharp
using System;

namespace AncorRA.AR
{
    public enum PaseoCardKind
    {
        None,
        Hint,
        Building
    }

    /// <summary>
    /// What the bottom card shows: the recognised building, or while searching a hint of what to do. The guide of the
    /// next phase replaces the single hint with step-by-step directions.
    /// </summary>
    public readonly struct PaseoCard : IEquatable<PaseoCard>
    {
        public const string HintTitle = "Apunta a un edificio del paseo";
        public const string HintSubtitle = "Muévete despacio frente a su fachada.";

        public readonly PaseoCardKind Kind;
        public readonly string Title;
        public readonly string Subtitle;

        PaseoCard(PaseoCardKind kind, string title, string subtitle)
        {
            Kind = kind;
            Title = title;
            Subtitle = subtitle;
        }

        public static PaseoCard None => default;

        public static PaseoCard Hint(string title, string subtitle) => new(PaseoCardKind.Hint, title, subtitle);

        public static PaseoCard Building(string name, string description) => new(PaseoCardKind.Building, name, description ?? "");

        public static PaseoCard Choose(bool sdkReady, bool tracking, string shownBuilding, string description)
        {
            if (!string.IsNullOrEmpty(shownBuilding))
                return Building(shownBuilding, description);
            return sdkReady && tracking ? Hint(HintTitle, HintSubtitle) : None;
        }

        public bool Equals(PaseoCard other) => Kind == other.Kind && Title == other.Title && Subtitle == other.Subtitle;

        public override bool Equals(object obj) => obj is PaseoCard other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind, Title, Subtitle);
    }
}
```

`PaseoCardChoice.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Which shown building gets the card: the one whose box centre is nearest the middle of the screen. Points come from
    /// Camera.WorldToViewportPoint: x and y run 0–1 across the screen and z &lt;= 0 means behind the camera.
    /// </summary>
    public static class PaseoCardChoice
    {
        // Up close a building fills the view while its centre sits a little off screen.
        public const float Margin = 0.25f;

        public static int Pick(IReadOnlyList<Vector3> viewportPoints)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < viewportPoints.Count; i++)
            {
                var p = viewportPoints[i];
                if (p.z <= 0f || p.x < -Margin || p.x > 1f + Margin || p.y < -Margin || p.y > 1f + Margin)
                    continue;
                float distance = (new Vector2(p.x, p.y) - new Vector2(0.5f, 0.5f)).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }
    }
}
```

`PaseoTapGesture.cs`:

```csharp
namespace AncorRA.AR
{
    /// <summary>Five quick taps on the top-left corner open or close the team sheet; a visitor never hits it by chance.</summary>
    public sealed class PaseoTapGesture
    {
        public const int Taps = 5;
        public const float WindowSeconds = 1.5f;

        int count;
        float last = float.NegativeInfinity;

        /// <summary>True on the tap that completes the series.</summary>
        public bool Tap(float now)
        {
            count = now - last < WindowSeconds ? count + 1 : 1;
            last = now;
            if (count < Taps)
                return false;
            count = 0;
            return true;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: el mismo comando del Step 2. Expected: 13 pasaron.

- [ ] **Step 5: Commit**

```bash
git add Assets/AncoRA/Scripts/Paseo/PaseoCard.cs* Assets/AncoRA/Scripts/Paseo/PaseoCardChoice.cs* Assets/AncoRA/Scripts/Paseo/PaseoTapGesture.cs* Assets/AncoRA/Editor/Tests/PaseoCardTests.cs* Assets/AncoRA/Editor/Tests/PaseoCardChoiceTests.cs* Assets/AncoRA/Editor/Tests/PaseoTapGestureTests.cs*
git commit -m "Decidir la tarjeta inferior y el edificio que la ocupa" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Descripción del edificio y caja sin texto 3D

**Files:**
- Modify: `Assets/AncoRA/Scripts/Paseo/PaseoConfig.cs` (clase `PaseoBuildingConfig`, `Parse`, `Validate`)
- Modify: `Assets/AncoRA/Scripts/Paseo/PaseoMapContent.cs` (sin `PaseoLabel`, con `Description`)
- Modify: `Assets/AncoRA/Editor/Paseo/PaseoSetup.cs` (`CreateContent`, su llamada, la línea de `font`, `ValidateScene`)
- Modify: `Assets/AncoRA/Editor/Tests/PaseoPoseProcessorTests.cs` (`AddFarBox`)
- Test: `Assets/AncoRA/Editor/Tests/PaseoConfigDescriptionTests.cs`

**Interfaces:**
- Produces: `PaseoBuildingConfig.descripcion` (string, nunca null tras `Parse`); `PaseoMapContent.Description`; `PaseoMapContent.Configure(int map, string building, string displayName, string buildingDescription, EdificioFacadeFrame frame)`.
- Removes: `PaseoMapContent.Label` y su parámetro en `Configure`.

- [ ] **Step 1: Write the failing test**

```csharp
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoConfigDescriptionTests
    {
        [Test]
        public void TheDescriptionIsRead()
        {
            var config = PaseoConfig.Parse("{\"nombre\":\"X1\",\"descripcion\":\"Edificio de laboratorios.\",\"mapas\":[]}");
            Assert.AreEqual("Edificio de laboratorios.", config.descripcion);
        }

        [Test]
        public void AMissingDescriptionIsEmptyAndWarned()
        {
            var config = PaseoConfig.Parse("{\"nombre\":\"X1\",\"mapas\":[]}");
            Assert.AreEqual("", config.descripcion);
            var validation = PaseoConfig.Validate("X1", config, new string[0], true);
            Assert.IsTrue(validation.Ok);
            Assert.IsTrue(validation.Warnings.Exists(w => w.Contains("descripcion")));
        }

        [Test]
        public void ADescriptionIsNotWarned()
        {
            var config = PaseoConfig.Parse("{\"nombre\":\"X1\",\"descripcion\":\"Algo.\",\"mapas\":[]}");
            Assert.IsFalse(PaseoConfig.Validate("X1", config, new string[0], true).Warnings.Exists(w => w.Contains("descripcion")));
        }

        [Test]
        public void ApplyingAFieldAdjustmentKeepsTheDescription()
        {
            var config = PaseoConfig.Parse("{\"nombre\":\"X1\",\"descripcion\":\"Algo.\",\"mapas\":[]}");
            Assert.AreEqual("Algo.", PaseoConfig.Parse(PaseoConfig.Serialize(config)).descripcion);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoConfigDescriptionTests`
Expected: no compila (`descripcion` no existe).

- [ ] **Step 3: Implement**

En `PaseoConfig.cs`, `PaseoBuildingConfig`:

```csharp
    public sealed class PaseoBuildingConfig
    {
        public string nombre;
        public string descripcion = "";
        public float[] tamano = { 20f, 8f, 12f };
        public bool solido;
        public List<PaseoMapConfig> mapas = new();
    }
```

En `Parse`, junto a `config.mapas ??= new List<PaseoMapConfig>();`:

```csharp
            config.descripcion ??= "";
```

En `Validate`, después del error de `tamano`:

```csharp
            if (string.IsNullOrWhiteSpace(config.descripcion))
                result.Warnings.Add($"{where}: sin «descripcion»; la tarjeta del edificio mostrará solo el nombre.");
```

`PaseoMapContent.cs` completo:

```csharp
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The content of one map: the building box, child of that map's XR Space. Its local pose is in the map's frame (the
    /// XR Map sits at identity inside its space). The name and description are drawn by the interface (PaseoUi).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaseoMapContent : MonoBehaviour
    {
        [SerializeField] int mapId;
        [SerializeField] string buildingId;
        [SerializeField] string buildingName;
        [SerializeField] string description;
        [SerializeField] EdificioFacadeFrame box;

        public int MapId => mapId;
        public string BuildingId => buildingId;
        public string BuildingName => buildingName;
        public string Description => description;
        public EdificioFacadeFrame Box => box;
        public bool Placed => box != null && box.PlacedByTeam;
        public Vector3 LocalPosition => transform.localPosition;
        public float LocalYaw => transform.localEulerAngles.y;
        public Vector3 SizeMeters => new(box.WidthMeters, box.HeightMeters, box.DepthMeters);

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(int map, string building, string displayName, string buildingDescription, EdificioFacadeFrame frame)
        {
            mapId = map;
            buildingId = building;
            buildingName = displayName;
            description = buildingDescription ?? "";
            box = frame;
        }

        public void SetPose(Vector3 localPosition, float yaw, bool placed)
        {
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            box.MarkPlaced(placed);
        }

        public void SetSize(Vector3 size, bool solid)
        {
            box.SetSize(size.x, size.y, size.z);
            box.SetSolid(solid);
        }

        public void SetVisible(bool visible) => box.SetVisible(visible);
    }
}
```

En `PaseoSetup.cs`: borrar `var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");`, cambiar la llamada a
`contents.Add(CreateContent(spaceObject.transform, pair.Key, pair.Value, mapConfig, border, fill, solid));` y reemplazar
`CreateContent` por:

```csharp
        static PaseoMapContent CreateContent(Transform space, string buildingId, PaseoBuildingConfig building, PaseoMapConfig mapConfig,
            Material border, Material fill, Material solid)
        {
            var boxObject = new GameObject("Caja");
            boxObject.transform.SetParent(space, false);
            var box = boxObject.AddComponent<EdificioFacadeFrame>();
            box.Configure(building.tamano[0], building.tamano[1], building.tamano[2], border, fill, solid, building.solido);

            var content = boxObject.AddComponent<PaseoMapContent>();
            content.Configure(mapConfig.id, buildingId, building.nombre, building.descripcion, box);
            var p = mapConfig.caja.posicion;
            content.SetPose(new Vector3(p[0], p[1], p[2]), mapConfig.caja.giro, mapConfig.caja.colocada);
            return content;
        }
```

En `ValidateScene`, reemplazar la comprobación de nombre:

```csharp
                if (content.BuildingId != building || content.BuildingName != config.nombre || content.Description != config.descripcion)
                    errors.Add($"{label}: la caja tiene edificio, nombre o descripción distintos a edificio.json; correr Prepare.");
```

y borrar las tres líneas del letrero (`var text = content.Label != null ? ...` y su `errors.Add`).

En `PaseoPoseProcessorTests.AddFarBox`: `content.Configure(1, "Prueba", "Prueba", "", frame);`

- [ ] **Step 4: Run the whole suite and Prepare**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests`
Expected: todo pasa (96 + 4 nuevas de este task + las de tasks anteriores).
Run: `...UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.Prepare` y luego `-Method AncorRA.Editor.PaseoSetup.Validate`
Expected: Validate OK con avisos «sin «descripcion»» para los cuatro edificios. (`PaseoHud` y `PaseoFieldAdjust` siguen
hasta el Task 8.)

- [ ] **Step 5: Commit**

```bash
git add Assets/AncoRA/Scripts/Paseo/PaseoConfig.cs Assets/AncoRA/Scripts/Paseo/PaseoMapContent.cs Assets/AncoRA/Editor/Paseo/PaseoSetup.cs Assets/AncoRA/Editor/Tests/PaseoPoseProcessorTests.cs Assets/AncoRA/Editor/Tests/PaseoConfigDescriptionTests.cs* Assets/Scenes/PaseoIngenieria.unity Assets/AncoRA/Paseo/_Materiales
git commit -m "Agregar la descripción del edificio y sacar el texto 3D de las cajas" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: El paseo espera la bienvenida y describe cada mapa

**Files:**
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoMapRow.cs`
- Modify: `Assets/AncoRA/Scripts/Paseo/PaseoTour.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoMapRowTests.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoTourBeginTests.cs`

**Interfaces:**
- Produces: `enum PaseoTone { Ok, Neutral, Warning, Error }`; `struct PaseoMapRowInputs` (campos abajo); `readonly struct PaseoMapRow` con `Title`, `Detail`, `Tone`, `static PaseoMapRow Build(PaseoMapRowInputs)`, `static readonly NumberFormatInfo Numbers`.
- Produces: `PaseoTour.WaitForBegin()`, `PaseoTour.Begin()`, `PaseoTour.Begun`, `PaseoTour.LocalizationSummary`, `IReadOnlyList<PaseoMapRow> PaseoTour.MapRows()`.

- [ ] **Step 1: Write the failing tests**

`PaseoMapRowTests.cs`:

```csharp
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoMapRowTests
    {
        static PaseoMapRowInputs Healthy() => new()
        {
            MapId = 152259, BuildingName = "Teología", Loaded = true, HasFilter = true, Placed = true
        };

        [Test]
        public void AMapThatDidNotLoadIsAnError()
        {
            var inputs = Healthy();
            inputs.Loaded = false;
            inputs.Failed = true;
            var row = PaseoMapRow.Build(inputs);
            Assert.AreEqual(PaseoTone.Error, row.Tone);
            StringAssert.Contains("NO CARGÓ", row.Detail);
        }

        [Test]
        public void AShownMapReadsItsLastLocalizationWithADecimalComma()
        {
            var inputs = Healthy();
            inputs.Located = true;
            inputs.Shown = true;
            inputs.Last = new PaseoPoseReport(PaseoPoseVerdict.Accepted, 38f, 0.6f, 0.23f, 18f);
            inputs.Used = 1;
            inputs.Seen = 2;
            var row = PaseoMapRow.Build(inputs);
            Assert.AreEqual("152259 Teología", row.Title);
            Assert.AreEqual("visible · aceptada, conf 38, movería 0,23 m · 1/2 usadas", row.Detail);
            Assert.AreEqual(PaseoTone.Ok, row.Tone);
        }

        [Test]
        public void BeforeTheFirstLockThereIsNoMove()
        {
            var inputs = Healthy();
            inputs.Last = new PaseoPoseReport(PaseoPoseVerdict.Waiting, 12f, 0.4f, float.NaN, 25f);
            inputs.Seen = 1;
            Assert.AreEqual("sin ubicar · esperando, conf 12 · 0/1 usadas", PaseoMapRow.Build(inputs).Detail);
        }

        [Test]
        public void AMapNotLocatedYetIsNeutral()
        {
            var row = PaseoMapRow.Build(Healthy());
            Assert.AreEqual("sin ubicar", row.Detail);
            Assert.AreEqual(PaseoTone.Neutral, row.Tone);
        }

        [Test]
        public void AnUnplacedBoxIsAWarning()
        {
            var inputs = Healthy();
            inputs.Placed = false;
            var row = PaseoMapRow.Build(inputs);
            Assert.AreEqual(PaseoTone.Warning, row.Tone);
            StringAssert.Contains("CAJA SIN COLOCAR", row.Detail);
        }
    }
}
```

`PaseoTourBeginTests.cs` (mismo montaje que `PaseoTourDiagnosticsTests`):

```csharp
using System;
using System.Reflection;
using AncorRA.AR;
using Immersal;
using Immersal.XR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AncorRA.Tests
{
    public class PaseoTourBeginTests
    {
        GameObject host;
        PaseoTour tour;
        UnityEvent<int> previousMapLoaded;

        [SetUp]
        public void SetUp()
        {
            previousMapLoaded = MapManager.MapRegisteredAndLoaded;
            host = new GameObject("Paseo de prueba");
            tour = host.AddComponent<PaseoTour>();
            var localizerHost = new GameObject("Localizer de prueba");
            localizerHost.transform.SetParent(host.transform);
            var localizer = localizerHost.AddComponent<Localizer>();
            localizer.OnLocalizationResult = new UnityEvent<ILocalizationResults>();
            var sdkHost = new GameObject("SDK de prueba");
            sdkHost.transform.SetParent(host.transform);
            sdkHost.SetActive(false);
            var sdk = sdkHost.AddComponent<ImmersalSDK>();
            tour.Configure(sdk, localizer, Array.Empty<PaseoMapContent>());
            Run("Awake");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            MapManager.MapRegisteredAndLoaded = previousMapLoaded;
        }

        void Run(string method) => typeof(PaseoTour).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tour, null);

        void StartedSecondsAgo(float seconds) => typeof(PaseoTour)
            .GetField("sceneStart", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(tour, Time.realtimeSinceStartup - seconds);

        [Test]
        public void WhileTheWelcomeIsOpenTheSdkTimeoutDoesNotFire()
        {
            tour.WaitForBegin();
            StartedSecondsAgo(20f);
            Run("CheckSdkStart");
            Assert.IsEmpty(tour.Error);
            Assert.IsFalse(tour.Begun);
        }

        [Test]
        public void BeginRestartsTheClockForTheSdkTimeout()
        {
            tour.WaitForBegin();
            StartedSecondsAgo(20f);
            tour.Begin();
            Assert.Less(tour.SecondsSinceStart, 1f);
            Run("CheckSdkStart");
            Assert.IsEmpty(tour.Error, "the minutes spent on the welcome do not count");

            StartedSecondsAgo(20f);
            LogAssert.Expect(LogType.Error, "[AncoRA Paseo] El SDK no quedó listo en 15 s; revisar el log de inicio.");
            Run("CheckSdkStart");
            Assert.IsNotEmpty(tour.Error);
        }

        [Test]
        public void ATourWithoutAWelcomeHasBegun()
        {
            Assert.IsTrue(tour.Begun);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter "AncorRA.Tests.PaseoMapRowTests|AncorRA.Tests.PaseoTourBeginTests"`
Expected: no compila; con esqueletos, fallan por comportamiento.

- [ ] **Step 3: Implement**

`PaseoMapRow.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;

namespace AncorRA.AR
{
    public enum PaseoTone
    {
        Ok,
        Neutral,
        Warning,
        Error
    }

    public struct PaseoMapRowInputs
    {
        public int MapId;
        public string BuildingName;
        public bool Loaded;
        public bool Failed;
        public bool Located;
        public bool Shown;
        public bool HasFilter;
        public bool Placed;
        public bool PhoneValues;
        public PaseoPoseReport? Last;
        public int Used;
        public int Seen;
    }

    /// <summary>One line of the team sheet per map, in Spanish, with a decimal comma whatever the phone's language.</summary>
    public readonly struct PaseoMapRow
    {
        public static readonly NumberFormatInfo Numbers = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

        public readonly string Title;
        public readonly string Detail;
        public readonly PaseoTone Tone;

        public PaseoMapRow(string title, string detail, PaseoTone tone)
        {
            Title = title;
            Detail = detail;
            Tone = tone;
        }

        public static PaseoMapRow Build(PaseoMapRowInputs s)
        {
            var parts = new List<string>();
            if (s.Failed)
                parts.Add("NO CARGÓ");
            else if (!s.Loaded)
                parts.Add("cargando");
            if (!s.HasFilter)
                parts.Add("SIN FILTRO");
            parts.Add(s.Shown ? "visible" : s.Located ? "ubicado" : "sin ubicar");
            if (s.Last is { } r)
            {
                string move = float.IsNaN(r.BoxMoveMeters) ? "" : string.Format(Numbers, ", movería {0:F2} m", r.BoxMoveMeters);
                parts.Add(string.Format(Numbers, "{0}, conf {1:F0}{2}", PaseoPoseProcessor.VerdictName(r.Verdict), r.Confidence, move));
            }
            if (s.Seen > 0)
                parts.Add($"{s.Used}/{s.Seen} usadas");
            if (!s.Placed)
                parts.Add("CAJA SIN COLOCAR");
            if (s.PhoneValues)
                parts.Add("valores del teléfono");

            var tone = s.Failed || !s.HasFilter ? PaseoTone.Error
                : !s.Placed || !s.Loaded ? PaseoTone.Warning
                : s.Located ? PaseoTone.Ok
                : PaseoTone.Neutral;
            return new PaseoMapRow($"{s.MapId} {s.BuildingName}", string.Join(" · ", parts), tone);
        }
    }
}
```

`PaseoTour.cs` — campos, junto a `int sdkRetries;`:

```csharp
        bool begun = true;
```

Propiedades públicas, junto a `SecondsSinceStart`:

```csharp
        public bool Begun => begun;
        public string LocalizationSummary => $"{localizationAttempts} intentos, {localizationFailures} sin coincidencia";

        /// <summary>The interface holds the tour until the welcome is closed and the permissions answered (PaseoUi).</summary>
        public void WaitForBegin() => begun = false;

        /// <summary>Starts the clocks of the SDK timeout and retry, so time spent on the welcome does not count.</summary>
        public void Begin()
        {
            if (begun)
                return;
            begun = true;
            sceneStart = Time.realtimeSinceStartup;
            Debug.Log($"{Tag} Comienza el paseo: bienvenida cerrada y permisos respondidos.");
        }

        public IReadOnlyList<PaseoMapRow> MapRows()
        {
            var rows = new List<PaseoMapRow>();
            foreach (var runtime in maps.Values)
            {
                var c = runtime.Content;
                var filter = runtime.Filter;
                bool hasFilter = filter != null;
                rows.Add(PaseoMapRow.Build(new PaseoMapRowInputs
                {
                    MapId = c.MapId,
                    BuildingName = c.BuildingName,
                    Loaded = mapLoad.IsLoaded(c.MapId),
                    Failed = mapLoad.IsFailed(c.MapId),
                    Located = visibility.IsLocated(c.MapId),
                    Shown = runtime.Shown,
                    HasFilter = hasFilter,
                    Placed = c.Placed,
                    PhoneValues = runtime.PhonePose || runtime.PhoneSize,
                    Last = hasFilter ? filter.LastReport : null,
                    Used = hasFilter ? filter.UsedResults : 0,
                    Seen = hasFilter ? filter.Results : 0
                }));
            }
            return rows;
        }
```

En `CheckSdkStart`, la condición de salida pasa a:

```csharp
            if (!begun || reportedTimeout || SecondsSinceStart < SdkStartTimeoutSeconds)
                return;
```

En `RetrySdkStart`, al principio (antes del bloque `IsFaulted`):

```csharp
            if (!begun)
                return;
```

- [ ] **Step 4: Run the whole suite**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests`
Expected: todo pasa, incluidas `PaseoTourDiagnosticsTests` (un paseo sin bienvenida empieza «comenzado»).

- [ ] **Step 5: Commit**

```bash
git add Assets/AncoRA/Scripts/Paseo/PaseoMapRow.cs* Assets/AncoRA/Scripts/Paseo/PaseoTour.cs Assets/AncoRA/Editor/Tests/PaseoMapRowTests.cs* Assets/AncoRA/Editor/Tests/PaseoTourBeginTests.cs*
git commit -m "Esperar la bienvenida antes de contar el arranque del SDK y describir cada mapa" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Vistas de bienvenida, cámara y nombres

**Files:**
- Create: `Assets/AncoRA/Scripts/Paseo/Ui/PaseoUiKit.cs`
- Create: `Assets/AncoRA/Scripts/Paseo/Ui/PaseoWelcomeView.cs`
- Create: `Assets/AncoRA/Scripts/Paseo/Ui/PaseoCameraView.cs`
- Create: `Assets/AncoRA/Scripts/Paseo/Ui/PaseoLabelsView.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoViewsTests.cs`

**Interfaces:**
- Consumes: `PaseoCard`, `PaseoCardKind` (Task 3); `PaseoStatusLevel` (existente).
- Produces: `PaseoUiKit.HiddenClass`, `StrongClass`, `Label Text(string, string, bool strong = false)`, `SetHidden(VisualElement, bool)`, `IsHidden(VisualElement)`, `ApplyFonts(VisualElement, Font regular, Font strong)`.
- Produces: `PaseoWelcomeView` (`Root`, `Visible`, `event Action Started`, `PressStart()`, `Show()`, `Hide()`, `ShowMessage(string)`, `DefaultNote`); `PaseoCameraView` (`Root`, `Card`, `event Action HelpRequested`, `PressHelp()`, `SetStatus(string, PaseoStatusLevel)`, `SetCard(PaseoCard)`, `EnteringClass`); `readonly struct PaseoLabelItem(string text, Vector2? position)`; `PaseoLabelsView` (`Root`, `Pills`, `bool Set(IReadOnlyList<PaseoLabelItem>)`).
- Element names usados por pruebas y por `PaseoUi`: `bienvenida`, `bienvenida-nota`, `comenzar`, `camara`, `estado`, `estado-texto`, `ayuda`, `tarjeta`, `tarjeta-titulo`, `tarjeta-chip`, `tarjeta-texto`, `nombres`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace AncorRA.Tests
{
    public class PaseoViewsTests
    {
        [Test]
        public void TheWelcomeOffersStartAndExplainsThePermissions()
        {
            var view = new PaseoWelcomeView();
            Assert.AreEqual("Comenzar", view.Root.Q<Button>("comenzar").text);
            Assert.AreEqual(PaseoWelcomeView.DefaultNote, view.Root.Q<Label>("bienvenida-nota").text);
            bool started = false;
            view.Started += () => started = true;
            view.PressStart();
            Assert.IsTrue(started);
        }

        [Test]
        public void TheWelcomeHidesAndComesBackWithItsNote()
        {
            var view = new PaseoWelcomeView();
            view.ShowMessage("Sin la cámara la app no puede funcionar.");
            view.Hide();
            Assert.IsFalse(view.Visible);
            view.Show();
            Assert.IsTrue(view.Visible);
            Assert.AreEqual(PaseoWelcomeView.DefaultNote, view.Root.Q<Label>("bienvenida-nota").text);
        }

        [Test]
        public void ThePillShowsTheStatusAndItsTone()
        {
            var view = new PaseoCameraView();
            view.SetStatus("Teología: ubicado", PaseoStatusLevel.Ok);
            Assert.AreEqual("Teología: ubicado", view.Root.Q<Label>("estado-texto").text);
            Assert.IsTrue(view.Root.Q("estado").ClassListContains("paseo-pill--ok"));
            view.SetStatus("Error: algo", PaseoStatusLevel.Error);
            Assert.IsFalse(view.Root.Q("estado").ClassListContains("paseo-pill--ok"));
            Assert.IsTrue(view.Root.Q("estado").ClassListContains("paseo-pill--error"));
        }

        [Test]
        public void NoCardMeansTheCardIsHidden()
        {
            var view = new PaseoCameraView();
            view.SetCard(PaseoCard.None);
            Assert.IsTrue(PaseoUiKit.IsHidden(view.Root.Q("tarjeta")));
        }

        [Test]
        public void ABuildingCardShowsNameChipAndDescription()
        {
            var view = new PaseoCameraView();
            view.SetCard(PaseoCard.Building("Teología", "Facultad de Teología."));
            Assert.IsFalse(PaseoUiKit.IsHidden(view.Root.Q("tarjeta")));
            Assert.AreEqual("Teología", view.Root.Q<Label>("tarjeta-titulo").text);
            Assert.AreEqual("Facultad de Teología.", view.Root.Q<Label>("tarjeta-texto").text);
            Assert.IsFalse(PaseoUiKit.IsHidden(view.Root.Q("tarjeta-chip")));
        }

        [Test]
        public void AHintHasNoChipAndAnEmptyDescriptionIsHidden()
        {
            var view = new PaseoCameraView();
            view.SetCard(PaseoCard.Hint(PaseoCard.HintTitle, ""));
            Assert.IsTrue(PaseoUiKit.IsHidden(view.Root.Q("tarjeta-chip")));
            Assert.IsTrue(PaseoUiKit.IsHidden(view.Root.Q("tarjeta-texto")));
        }

        [Test]
        public void ANewCardSlidesInButTheSameCardDoesNotReplay()
        {
            var view = new PaseoCameraView();
            var card = view.Root.Q("tarjeta");
            view.SetCard(PaseoCard.Building("X1", ""));
            Assert.IsTrue(card.ClassListContains(PaseoCameraView.EnteringClass));
            card.RemoveFromClassList(PaseoCameraView.EnteringClass); // what the scheduled step does on device
            view.SetCard(PaseoCard.Building("X1", ""));
            Assert.IsFalse(card.ClassListContains(PaseoCameraView.EnteringClass));
            view.SetCard(PaseoCard.Building("EIC", ""));
            Assert.IsTrue(card.ClassListContains(PaseoCameraView.EnteringClass));
        }

        [Test]
        public void HelpBringsTheWelcomeBack()
        {
            var view = new PaseoCameraView();
            bool asked = false;
            view.HelpRequested += () => asked = true;
            view.PressHelp();
            Assert.IsTrue(asked);
        }

        [Test]
        public void NamePillsFollowTheirBoxesAndHideWithoutAPosition()
        {
            var view = new PaseoLabelsView();
            bool created = view.Set(new List<PaseoLabelItem>
            {
                new("Teología", new Vector2(120f, 300f)),
                new("X1", null)
            });
            Assert.IsTrue(created);
            Assert.AreEqual(2, view.Pills.Count);
            Assert.AreEqual("Teología", view.Pills[0].text);
            Assert.AreEqual(120f, view.Pills[0].style.left.value.value, 1e-3f);
            Assert.AreEqual(300f, view.Pills[0].style.top.value.value, 1e-3f);
            Assert.IsFalse(PaseoUiKit.IsHidden(view.Pills[0]));
            Assert.IsTrue(PaseoUiKit.IsHidden(view.Pills[1]));

            Assert.IsFalse(view.Set(new List<PaseoLabelItem> { new("Teología", new Vector2(1f, 2f)) }));
            Assert.IsTrue(PaseoUiKit.IsHidden(view.Pills[1]), "a pill without an item hides");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoViewsTests`
Expected: no compila (vistas inexistentes).

- [ ] **Step 3: Implement**

`Ui/PaseoUiKit.cs`:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace AncorRA.AR
{
    /// <summary>Small helpers shared by the tour views: labels with classes, hiding, and the two Inter weights.</summary>
    public static class PaseoUiKit
    {
        public const string HiddenClass = "is-hidden";
        public const string StrongClass = "is-strong";

        public static Label Text(string text, string className, bool strong = false)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            if (strong)
                label.AddToClassList(StrongClass);
            return label;
        }

        public static void SetHidden(VisualElement element, bool hidden) => element.EnableInClassList(HiddenClass, hidden);

        public static bool IsHidden(VisualElement element) => element.ClassListContains(HiddenClass);

        /// <summary>
        /// Sets Inter on every text element below root (semibold on the ones marked strong). Inline styles win over the
        /// default theme, which sets its own font on text elements. Call again after adding text elements.
        /// </summary>
        public static void ApplyFonts(VisualElement root, Font regular, Font strong)
        {
            if (regular == null || strong == null)
                return;
            var regularDefinition = new StyleFontDefinition(FontDefinition.FromFont(regular));
            var strongDefinition = new StyleFontDefinition(FontDefinition.FromFont(strong));
            root.Query<TextElement>().ForEach(e =>
                e.style.unityFontDefinition = e.ClassListContains(StrongClass) ? strongDefinition : regularDefinition);
        }
    }
}
```

`Ui/PaseoWelcomeView.cs`:

```csharp
using System;
using UnityEngine.UIElements;

namespace AncorRA.AR
{
    /// <summary>
    /// Welcome screen: what the tour is, three steps, and the button that asks for the camera and the location. Built in
    /// code (no UXML) so EditMode tests can build and read it without a panel.
    /// </summary>
    public sealed class PaseoWelcomeView
    {
        public const string DefaultNote = "Usa la cámara y tu ubicación.";

        static readonly string[] Steps =
        {
            "Camina al punto que te indicamos.",
            "Apunta al edificio, sin apuro.",
            "Míralo en realidad aumentada."
        };

        readonly Label note;

        public event Action Started;

        public VisualElement Root { get; }
        public bool Visible => !PaseoUiKit.IsHidden(Root);

        public PaseoWelcomeView()
        {
            Root = new VisualElement { name = "bienvenida" };
            Root.AddToClassList("paseo-welcome");
            Root.Add(PaseoUiKit.Text("AncoRA", "paseo-welcome__title", strong: true));
            Root.Add(PaseoUiKit.Text("Paseo por la Escuela de Ingeniería", "paseo-welcome__subtitle"));
            for (int i = 0; i < Steps.Length; i++)
            {
                var step = new VisualElement();
                step.AddToClassList("paseo-step");
                step.Add(PaseoUiKit.Text((i + 1).ToString(), "paseo-step__badge", strong: true));
                step.Add(PaseoUiKit.Text(Steps[i], "paseo-step__text"));
                Root.Add(step);
            }
            var spacer = new VisualElement();
            spacer.AddToClassList("paseo-welcome__spacer");
            Root.Add(spacer);
            note = PaseoUiKit.Text(DefaultNote, "paseo-welcome__note");
            note.name = "bienvenida-nota";
            Root.Add(note);
            var start = new Button(PressStart) { name = "comenzar", text = "Comenzar" };
            start.AddToClassList("paseo-button");
            start.AddToClassList("paseo-button--primary");
            start.AddToClassList(PaseoUiKit.StrongClass);
            Root.Add(start);
        }

        public void PressStart() => Started?.Invoke();

        public void Show()
        {
            note.text = DefaultNote;
            PaseoUiKit.SetHidden(Root, false);
        }

        public void Hide() => PaseoUiKit.SetHidden(Root, true);

        public void ShowMessage(string message) => note.text = message;
    }
}
```

`Ui/PaseoCameraView.cs`:

```csharp
using System;
using UnityEngine.UIElements;

namespace AncorRA.AR
{
    /// <summary>
    /// The overlay on the camera: status pill on top, the card at the bottom (a hint, or the building that was
    /// recognised) and a help button that brings the welcome back.
    /// </summary>
    public sealed class PaseoCameraView
    {
        public const string EnteringClass = "paseo-card--entering";

        readonly VisualElement pill;
        readonly Label status;
        readonly VisualElement card;
        readonly Label title;
        readonly Label chip;
        readonly Label subtitle;

        public event Action HelpRequested;

        public VisualElement Root { get; }
        public PaseoCard Card { get; private set; } = PaseoCard.None;

        public PaseoCameraView()
        {
            Root = new VisualElement { name = "camara", pickingMode = PickingMode.Ignore };
            Root.AddToClassList("paseo-camera");

            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.AddToClassList("paseo-top");
            pill = new VisualElement { name = "estado" };
            pill.AddToClassList("paseo-pill");
            var dot = new VisualElement();
            dot.AddToClassList("paseo-pill__dot");
            pill.Add(dot);
            status = PaseoUiKit.Text("", "paseo-pill__text");
            status.name = "estado-texto";
            pill.Add(status);
            top.Add(pill);
            Root.Add(top);

            var help = new Button(PressHelp) { name = "ayuda", text = "?" };
            help.AddToClassList("paseo-help");
            help.AddToClassList(PaseoUiKit.StrongClass);
            Root.Add(help);

            var bottom = new VisualElement { pickingMode = PickingMode.Ignore };
            bottom.AddToClassList("paseo-bottom");
            card = new VisualElement { name = "tarjeta" };
            card.AddToClassList("paseo-card");
            var row = new VisualElement();
            row.AddToClassList("paseo-card__row");
            title = PaseoUiKit.Text("", "paseo-card__title", strong: true);
            title.name = "tarjeta-titulo";
            chip = PaseoUiKit.Text("Ubicado", "paseo-chip", strong: true);
            chip.name = "tarjeta-chip";
            row.Add(title);
            row.Add(chip);
            card.Add(row);
            subtitle = PaseoUiKit.Text("", "paseo-card__subtitle");
            subtitle.name = "tarjeta-texto";
            card.Add(subtitle);
            bottom.Add(card);
            Root.Add(bottom);
            PaseoUiKit.SetHidden(card, true);
        }

        public void PressHelp() => HelpRequested?.Invoke();

        public void SetStatus(string text, PaseoStatusLevel level)
        {
            status.text = text;
            pill.EnableInClassList("paseo-pill--ok", level == PaseoStatusLevel.Ok);
            pill.EnableInClassList("paseo-pill--error", level == PaseoStatusLevel.Error);
        }

        public void SetCard(PaseoCard next)
        {
            if (next.Equals(Card))
                return;
            bool newContent = next.Kind != Card.Kind || next.Title != Card.Title;
            Card = next;
            PaseoUiKit.SetHidden(card, next.Kind == PaseoCardKind.None);
            if (next.Kind == PaseoCardKind.None)
                return;
            title.text = next.Title;
            subtitle.text = next.Subtitle ?? "";
            PaseoUiKit.SetHidden(subtitle, string.IsNullOrEmpty(next.Subtitle));
            PaseoUiKit.SetHidden(chip, next.Kind != PaseoCardKind.Building);
            if (!newContent)
                return;
            // Start below and transparent; the USS transition brings it up once the class goes on the next frames.
            card.AddToClassList(EnteringClass);
            card.schedule.Execute(() => card.RemoveFromClassList(EnteringClass)).ExecuteLater(30);
        }
    }
}
```

`Ui/PaseoLabelsView.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AncorRA.AR
{
    public readonly struct PaseoLabelItem
    {
        public readonly string Text;
        public readonly Vector2? Position;

        public PaseoLabelItem(string text, Vector2? position)
        {
            Text = text;
            Position = position;
        }
    }

    /// <summary>
    /// Name pills pinned over the boxes. Positions are in panel coordinates (the pill's bottom centre); null hides the
    /// pill (box hidden or behind the camera). Pills are reused frame to frame.
    /// </summary>
    public sealed class PaseoLabelsView
    {
        readonly List<Label> pills = new();

        public VisualElement Root { get; }
        public IReadOnlyList<Label> Pills => pills;

        public PaseoLabelsView()
        {
            Root = new VisualElement { name = "nombres", pickingMode = PickingMode.Ignore };
            Root.AddToClassList("paseo-labels");
        }

        /// <returns>True when a pill was created, so the caller applies the fonts to it.</returns>
        public bool Set(IReadOnlyList<PaseoLabelItem> items)
        {
            bool created = false;
            while (pills.Count < items.Count)
            {
                var pill = PaseoUiKit.Text("", "paseo-label", strong: true);
                pill.pickingMode = PickingMode.Ignore;
                pills.Add(pill);
                Root.Add(pill);
                created = true;
            }
            for (int i = 0; i < pills.Count; i++)
            {
                var pill = pills[i];
                bool show = i < items.Count && items[i].Position.HasValue;
                PaseoUiKit.SetHidden(pill, !show);
                if (!show)
                    continue;
                var position = items[i].Position.Value;
                pill.text = items[i].Text;
                pill.style.left = position.x;
                pill.style.top = position.y;
            }
            return created;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoViewsTests`
Expected: 9 pasaron.

- [ ] **Step 5: Commit**

```bash
git add Assets/AncoRA/Scripts/Paseo/Ui* Assets/AncoRA/Editor/Tests/PaseoViewsTests.cs*
git commit -m "Armar las vistas de bienvenida, cámara y nombres en UI Toolkit" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Hoja del equipo

**Files:**
- Create: `Assets/AncoRA/Scripts/Paseo/Ui/PaseoTeamSheet.cs`
- Test: `Assets/AncoRA/Editor/Tests/PaseoTeamSheetTests.cs`

**Interfaces:**
- Consumes: `PaseoUiKit` (Task 6), `PaseoMapRow`, `PaseoTone` (Task 5).
- Produces: `enum PaseoSheetAction { Left, Right, Closer, Farther, Down, Up, TurnLeft, TurnRight, Narrower, Wider, Lower, Taller, Shallower, Deeper, ToggleSolid, NextStep, Copy, Restore, Close }`; `readonly struct PaseoBoxEdit` (`Right`, `Closer`, `Up`, `Turn`, `Width`, `Height`, `Depth`, `ToggleSolid`, ctor con argumentos nombrados opcionales); `PaseoTeamSheet` (`Root`, `IsOpen`, `StepName`, `ClosedClass`, `RestoreConfirmSeconds`, eventos `EditRequested`, `CopyRequested`, `RestoreRequested`, `Toggle()`, `Close()`, `ShowToast(string)`, `bool SetStatus(string, IReadOnlyList<PaseoMapRow>)`, `SetTarget(string)`, `Press(PaseoSheetAction, float now)`, `static string ToneClass(PaseoTone)`).
- Element names: `equipo`, `equipo-resumen`, `equipo-mapas`, `equipo-objetivo`, `equipo-sin-objetivo`, `equipo-controles`, `equipo-paso`, `equipo-aviso`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace AncorRA.Tests
{
    public class PaseoTeamSheetTests
    {
        PaseoTeamSheet sheet;
        readonly List<PaseoBoxEdit> edits = new();

        [SetUp]
        public void SetUp()
        {
            edits.Clear();
            sheet = new PaseoTeamSheet();
            sheet.EditRequested += edits.Add;
        }

        [Test]
        public void ItStartsClosedAndToggles()
        {
            Assert.IsFalse(sheet.IsOpen);
            sheet.Toggle();
            Assert.IsTrue(sheet.IsOpen);
            sheet.Press(PaseoSheetAction.Close, 0f);
            Assert.IsFalse(sheet.IsOpen);
        }

        [Test]
        public void WithoutALocatedBuildingTheAdjustmentSaysSoAndDoesNothing()
        {
            sheet.SetTarget(null);
            sheet.Press(PaseoSheetAction.Right, 0f);
            Assert.IsEmpty(edits);
            Assert.IsTrue(PaseoUiKit.IsHidden(sheet.Root.Q("equipo-controles")));
            Assert.IsFalse(PaseoUiKit.IsHidden(sheet.Root.Q("equipo-sin-objetivo")));
        }

        [Test]
        public void RightAtTheMediumStepMovesAQuarterMetre()
        {
            sheet.SetTarget("Teología · mapa 152259");
            Assert.AreEqual("medio", sheet.StepName);
            sheet.Press(PaseoSheetAction.Right, 0f);
            Assert.AreEqual(1, edits.Count);
            Assert.AreEqual(0.25f, edits[0].Right, 1e-5f);
            Assert.AreEqual(0f, edits[0].Closer);
        }

        [Test]
        public void TheStepCyclesMediumFineCoarse()
        {
            sheet.SetTarget("X1 · mapa 152198");
            sheet.Press(PaseoSheetAction.NextStep, 0f);
            Assert.AreEqual("fino", sheet.StepName);
            sheet.Press(PaseoSheetAction.TurnLeft, 0f);
            Assert.AreEqual(-0.2f, edits[0].Turn, 1e-5f);
            sheet.Press(PaseoSheetAction.NextStep, 0f);
            Assert.AreEqual("grueso", sheet.StepName);
            sheet.Press(PaseoSheetAction.Wider, 0f);
            Assert.AreEqual(2f, edits[1].Width, 1e-5f);
            sheet.Press(PaseoSheetAction.Farther, 0f);
            Assert.AreEqual(-1f, edits[2].Closer, 1e-5f);
        }

        [Test]
        public void RestoreNeedsASecondTapWithinThreeSeconds()
        {
            int restores = 0;
            sheet.RestoreRequested += () => restores++;
            sheet.Press(PaseoSheetAction.Restore, 0f);
            Assert.AreEqual(0, restores);
            sheet.Press(PaseoSheetAction.Restore, 1f);
            Assert.AreEqual(1, restores);
            sheet.Press(PaseoSheetAction.Restore, 10f);
            sheet.Press(PaseoSheetAction.Restore, 10f + PaseoTeamSheet.RestoreConfirmSeconds + 0.1f);
            Assert.AreEqual(1, restores, "the second tap came too late: it only arms again");
        }

        [Test]
        public void CopyIsAsked()
        {
            bool copied = false;
            sheet.CopyRequested += () => copied = true;
            sheet.Press(PaseoSheetAction.Copy, 0f);
            Assert.IsTrue(copied);
        }

        [Test]
        public void OneRowPerMapWithItsTone()
        {
            bool created = sheet.SetStatus("v1.2.0", new[]
            {
                new PaseoMapRow("152259 Teología", "visible", PaseoTone.Ok),
                new PaseoMapRow("152242 EIC", "NO CARGÓ", PaseoTone.Error)
            });
            Assert.IsTrue(created);
            var rows = sheet.Root.Q("equipo-mapas");
            Assert.AreEqual(2, rows.childCount);
            Assert.IsTrue(rows[1][1].ClassListContains(PaseoTeamSheet.ToneClass(PaseoTone.Error)));
            Assert.IsFalse(rows[1][1].ClassListContains(PaseoTeamSheet.ToneClass(PaseoTone.Ok)));

            Assert.IsFalse(sheet.SetStatus("v1.2.0", new[] { new PaseoMapRow("152259 Teología", "visible", PaseoTone.Ok) }));
            Assert.IsTrue(PaseoUiKit.IsHidden(rows[1]));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoTeamSheetTests`
Expected: no compila.

- [ ] **Step 3: Implement**

`Ui/PaseoTeamSheet.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AncorRA.AR
{
    public enum PaseoSheetAction
    {
        Left, Right, Closer, Farther, Down, Up, TurnLeft, TurnRight,
        Narrower, Wider, Lower, Taller, Shallower, Deeper, ToggleSolid,
        NextStep, Copy, Restore, Close
    }

    /// <summary>A change the team asked for, already multiplied by the step: metres and degrees.</summary>
    public readonly struct PaseoBoxEdit
    {
        public readonly float Right;
        public readonly float Closer;
        public readonly float Up;
        public readonly float Turn;
        public readonly float Width;
        public readonly float Height;
        public readonly float Depth;
        public readonly bool ToggleSolid;

        public PaseoBoxEdit(float right = 0f, float closer = 0f, float up = 0f, float turn = 0f, float width = 0f,
            float height = 0f, float depth = 0f, bool toggleSolid = false)
        {
            Right = right;
            Closer = closer;
            Up = up;
            Turn = turn;
            Width = width;
            Height = height;
            Depth = depth;
            ToggleSolid = toggleSolid;
        }
    }

    /// <summary>
    /// Team sheet: slides up from the bottom with the state of every map and the box adjustment, moved in the box's own
    /// axes (PaseoBoxNudge). Buttons call Press, so tests drive it without a panel.
    /// </summary>
    public sealed class PaseoTeamSheet
    {
        public const string ClosedClass = "paseo-sheet--closed";
        public const float RestoreConfirmSeconds = 3f;

        static readonly float[] PositionSteps = { 1f, 0.25f, 0.05f };
        static readonly float[] AngleSteps = { 5f, 1f, 0.2f };
        static readonly float[] SizeSteps = { 2f, 0.5f, 0.1f };
        static readonly string[] StepNames = { "grueso", "medio", "fino" };

        readonly Label summary;
        readonly VisualElement rows;
        readonly Label target;
        readonly Label noTarget;
        readonly VisualElement controls;
        readonly Button stepButton;
        readonly Label toast;
        int stepIndex = 1;
        bool hasTarget;
        float restoreArmedUntil = float.NegativeInfinity;

        public event Action<PaseoBoxEdit> EditRequested;
        public event Action CopyRequested;
        public event Action RestoreRequested;

        public VisualElement Root { get; }
        public bool IsOpen => !Root.ClassListContains(ClosedClass);
        public string StepName => StepNames[stepIndex];

        public PaseoTeamSheet()
        {
            Root = new VisualElement { name = "equipo" };
            Root.AddToClassList("paseo-sheet");
            Root.AddToClassList(ClosedClass);
            var grabber = new VisualElement();
            grabber.AddToClassList("paseo-sheet__grabber");
            Root.Add(grabber);
            var header = new VisualElement();
            header.AddToClassList("paseo-sheet__header");
            header.Add(PaseoUiKit.Text("Equipo", "paseo-sheet__title", strong: true));
            header.Add(ActionButton("Cerrar", PaseoSheetAction.Close, "paseo-link"));
            Root.Add(header);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("paseo-sheet__scroll");
            Root.Add(scroll);

            var state = Section(scroll, "Estado");
            summary = PaseoUiKit.Text("", "paseo-sheet__summary");
            summary.name = "equipo-resumen";
            state.Add(summary);
            rows = new VisualElement { name = "equipo-mapas" };
            state.Add(rows);

            var adjust = Section(scroll, "Ajuste de caja");
            target = PaseoUiKit.Text("", "paseo-sheet__target", strong: true);
            target.name = "equipo-objetivo";
            adjust.Add(target);
            noTarget = PaseoUiKit.Text("Ubícate con un edificio para ajustar su caja.", "paseo-sheet__hint");
            noTarget.name = "equipo-sin-objetivo";
            adjust.Add(noTarget);
            controls = new VisualElement { name = "equipo-controles" };
            adjust.Add(controls);
            stepButton = ActionButton("", PaseoSheetAction.NextStep, "paseo-nudge");
            stepButton.name = "equipo-paso";
            stepButton.AddToClassList("paseo-nudge--wide");
            controls.Add(stepButton);
            var grid = new VisualElement();
            grid.AddToClassList("paseo-nudges");
            controls.Add(grid);
            foreach (var (text, action) in new[]
                     {
                         ("Izquierda", PaseoSheetAction.Left), ("Derecha", PaseoSheetAction.Right),
                         ("Más lejos", PaseoSheetAction.Farther), ("Más cerca", PaseoSheetAction.Closer),
                         ("Bajar", PaseoSheetAction.Down), ("Subir", PaseoSheetAction.Up),
                         ("Girar a la izquierda", PaseoSheetAction.TurnLeft), ("Girar a la derecha", PaseoSheetAction.TurnRight),
                         ("Más angosta", PaseoSheetAction.Narrower), ("Más ancha", PaseoSheetAction.Wider),
                         ("Más baja", PaseoSheetAction.Lower), ("Más alta", PaseoSheetAction.Taller),
                         ("Menos fondo", PaseoSheetAction.Shallower), ("Más fondo", PaseoSheetAction.Deeper)
                     })
                grid.Add(ActionButton(text, action, "paseo-nudge"));
            var solid = ActionButton("Relleno: sólido o transparente", PaseoSheetAction.ToggleSolid, "paseo-nudge");
            solid.AddToClassList("paseo-nudge--wide");
            controls.Add(solid);

            var footer = new VisualElement();
            footer.AddToClassList("paseo-sheet__footer");
            scroll.Add(footer);
            footer.Add(ActionButton("Copiar valores", PaseoSheetAction.Copy, "paseo-button"));
            footer.Add(ActionButton("Restaurar escena", PaseoSheetAction.Restore, "paseo-button"));
            toast = PaseoUiKit.Text("", "paseo-toast");
            toast.name = "equipo-aviso";
            scroll.Add(toast);

            SetTarget(null);
            UpdateStepButton();
        }

        public void Toggle() => Root.EnableInClassList(ClosedClass, IsOpen);

        public void Close() => Root.AddToClassList(ClosedClass);

        public void ShowToast(string message) => toast.text = message;

        public static string ToneClass(PaseoTone tone) => "tone-" + tone.ToString().ToLowerInvariant();

        /// <returns>True when a row was created, so the caller applies the fonts to it.</returns>
        public bool SetStatus(string text, IReadOnlyList<PaseoMapRow> mapRows)
        {
            summary.text = text;
            bool created = false;
            while (rows.childCount < mapRows.Count)
            {
                var row = new VisualElement();
                row.AddToClassList("paseo-row");
                row.Add(PaseoUiKit.Text("", "paseo-row__title", strong: true));
                row.Add(PaseoUiKit.Text("", "paseo-row__detail"));
                rows.Add(row);
                created = true;
            }
            for (int i = 0; i < rows.childCount; i++)
            {
                var row = rows[i];
                bool used = i < mapRows.Count;
                PaseoUiKit.SetHidden(row, !used);
                if (!used)
                    continue;
                ((Label)row[0]).text = mapRows[i].Title;
                var detail = (Label)row[1];
                detail.text = mapRows[i].Detail;
                foreach (PaseoTone tone in Enum.GetValues(typeof(PaseoTone)))
                    detail.EnableInClassList(ToneClass(tone), tone == mapRows[i].Tone);
            }
            return created;
        }

        public void SetTarget(string description)
        {
            hasTarget = !string.IsNullOrEmpty(description);
            target.text = description ?? "";
            PaseoUiKit.SetHidden(target, !hasTarget);
            PaseoUiKit.SetHidden(controls, !hasTarget);
            PaseoUiKit.SetHidden(noTarget, hasTarget);
        }

        public void Press(PaseoSheetAction action, float now)
        {
            switch (action)
            {
                case PaseoSheetAction.Close:
                    Close();
                    return;
                case PaseoSheetAction.NextStep:
                    stepIndex = (stepIndex + 1) % StepNames.Length;
                    UpdateStepButton();
                    return;
                case PaseoSheetAction.Copy:
                    CopyRequested?.Invoke();
                    return;
                case PaseoSheetAction.Restore:
                    if (now < restoreArmedUntil)
                    {
                        restoreArmedUntil = float.NegativeInfinity;
                        RestoreRequested?.Invoke();
                    }
                    else
                    {
                        restoreArmedUntil = now + RestoreConfirmSeconds;
                        ShowToast("Toca otra vez para borrar lo guardado en este teléfono.");
                    }
                    return;
            }
            if (!hasTarget)
                return;
            float p = PositionSteps[stepIndex], a = AngleSteps[stepIndex], s = SizeSteps[stepIndex];
            EditRequested?.Invoke(action switch
            {
                PaseoSheetAction.Left => new PaseoBoxEdit(right: -p),
                PaseoSheetAction.Right => new PaseoBoxEdit(right: p),
                PaseoSheetAction.Closer => new PaseoBoxEdit(closer: p),
                PaseoSheetAction.Farther => new PaseoBoxEdit(closer: -p),
                PaseoSheetAction.Down => new PaseoBoxEdit(up: -p),
                PaseoSheetAction.Up => new PaseoBoxEdit(up: p),
                PaseoSheetAction.TurnLeft => new PaseoBoxEdit(turn: -a),
                PaseoSheetAction.TurnRight => new PaseoBoxEdit(turn: a),
                PaseoSheetAction.Narrower => new PaseoBoxEdit(width: -s),
                PaseoSheetAction.Wider => new PaseoBoxEdit(width: s),
                PaseoSheetAction.Lower => new PaseoBoxEdit(height: -s),
                PaseoSheetAction.Taller => new PaseoBoxEdit(height: s),
                PaseoSheetAction.Shallower => new PaseoBoxEdit(depth: -s),
                PaseoSheetAction.Deeper => new PaseoBoxEdit(depth: s),
                PaseoSheetAction.ToggleSolid => new PaseoBoxEdit(toggleSolid: true),
                _ => default
            });
        }

        Button ActionButton(string text, PaseoSheetAction action, string className)
        {
            var button = new Button(() => Press(action, Time.realtimeSinceStartup)) { text = text };
            button.AddToClassList(className);
            button.AddToClassList(PaseoUiKit.StrongClass);
            return button;
        }

        static VisualElement Section(VisualElement parent, string title)
        {
            var section = new VisualElement();
            section.AddToClassList("paseo-section");
            section.Add(PaseoUiKit.Text(title, "paseo-section__title"));
            parent.Add(section);
            return section;
        }

        void UpdateStepButton() => stepButton.text = $"Paso: {StepNames[stepIndex]}";
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityBatch.ps1 -Mode tests -Filter AncorRA.Tests.PaseoTeamSheetTests`
Expected: 7 pasaron.

- [ ] **Step 5: Commit**

```bash
git add Assets/AncoRA/Scripts/Paseo/Ui/PaseoTeamSheet.cs* Assets/AncoRA/Editor/Tests/PaseoTeamSheetTests.cs*
git commit -m "Armar la hoja del equipo con el ajuste de caja en sus propios ejes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Conectar la interfaz y retirar IMGUI

**Files:**
- Create: `Assets/AncoRA/Paseo/_Ui/Paseo.uss`, `Assets/AncoRA/Paseo/_Ui/PaseoTema.tss`
- Create (copia): `Assets/AncoRA/Paseo/_Ui/Fuentes/Inter-Regular.ttf`, `Inter-SemiBold.ttf`, `Inter-LICENSE.txt`
- Create (lo genera `Prepare`): `Assets/AncoRA/Paseo/_Ui/PaseoPanel.asset`
- Create: `Assets/AncoRA/Scripts/Paseo/PaseoUi.cs`
- Modify: `Assets/AncoRA/Editor/Paseo/PaseoSetup.cs` (Prepare: interfaz; Validate: interfaz)
- Modify: `Assets/AncoRA/Scripts/GuiSafeArea.cs` (comentario: ya no es solo IMGUI)
- Delete: `Assets/AncoRA/Scripts/Paseo/PaseoHud.cs`, `PaseoFieldAdjust.cs`, `PaseoLabel.cs` (y sus `.meta`)

**Interfaces:**
- Consumes: todo lo anterior; `PaseoTour.WaitForBegin/Begin/MapRows/LocalizationSummary/EditableContent/SaveEdit/ForgetSavedAndReload/BuildAdjustment/Contents`.
- Produces: `PaseoUi.Configure(PaseoTour, ARSession, StyleSheet, Font regular, Font strong)` y propiedades `Tour`, `ArSession`, `StyleSheet`, `RegularFont`, `StrongFont`.

- [ ] **Step 1: Copy the fonts**

```powershell
New-Item -ItemType Directory -Force Assets/AncoRA/Paseo/_Ui/Fuentes | Out-Null
$fonts = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Resources\Fonts'
Copy-Item "$fonts\Inter-Regular.ttf","$fonts\Inter-SemiBold.ttf","$fonts\Inter-LICENSE.txt" Assets/AncoRA/Paseo/_Ui/Fuentes/
```

- [ ] **Step 2: Write the theme and the style sheet**

`Assets/AncoRA/Paseo/_Ui/PaseoTema.tss`:

```css
@import url("unity-theme://default");
```

`Assets/AncoRA/Paseo/_Ui/Paseo.uss`:

```css
.paseo-root { flex-grow: 1; }
.is-hidden { display: none; }
.paseo-safe { position: absolute; left: 0; top: 0; right: 0; bottom: 0; }

/* Welcome: full screen, light, iOS-like onboarding. */
.paseo-welcome { position: absolute; left: 0; top: 0; right: 0; bottom: 0; padding: 64px 28px 36px 28px; background-color: rgb(249, 249, 251); }
.paseo-welcome__title { font-size: 40px; color: rgb(17, 17, 17); margin-top: 24px; }
.paseo-welcome__subtitle { font-size: 17px; color: rgb(110, 110, 115); margin-top: 4px; margin-bottom: 40px; white-space: normal; }
.paseo-step { flex-direction: row; align-items: center; margin-bottom: 22px; }
.paseo-step__badge { width: 34px; height: 34px; border-radius: 17px; flex-shrink: 0; margin-right: 14px; background-color: rgb(0, 122, 255); color: rgb(255, 255, 255); font-size: 16px; -unity-text-align: middle-center; }
.paseo-step__text { font-size: 17px; color: rgb(17, 17, 17); white-space: normal; flex-shrink: 1; }
.paseo-welcome__spacer { flex-grow: 1; }
.paseo-welcome__note { font-size: 13px; color: rgb(142, 142, 147); -unity-text-align: middle-center; white-space: normal; margin-bottom: 12px; }

/* Buttons (the default theme styles buttons too: hover and active are restated). */
.paseo-button { height: 50px; margin: 6px 0 0 0; border-width: 0; border-radius: 14px; font-size: 17px; background-color: rgb(229, 229, 234); color: rgb(0, 122, 255); }
.paseo-button:hover { background-color: rgb(229, 229, 234); }
.paseo-button:active { background-color: rgb(209, 209, 214); }
.paseo-button--primary, .paseo-button--primary:hover { background-color: rgb(0, 122, 255); color: rgb(255, 255, 255); }
.paseo-button--primary:active { background-color: rgb(0, 100, 214); }

/* Camera overlay */
.paseo-camera { position: absolute; left: 0; top: 0; right: 0; bottom: 0; }
.paseo-top { position: absolute; left: 0; right: 0; top: 6px; align-items: center; }
.paseo-pill { flex-direction: row; align-items: center; max-width: 76%; padding: 8px 14px; border-radius: 18px; background-color: rgba(250, 250, 252, 0.94); }
.paseo-pill__dot { width: 8px; height: 8px; border-radius: 4px; flex-shrink: 0; margin-right: 8px; background-color: rgb(255, 159, 10); }
.paseo-pill--ok .paseo-pill__dot { background-color: rgb(52, 199, 89); }
.paseo-pill--error .paseo-pill__dot { background-color: rgb(255, 59, 48); }
.paseo-pill__text { font-size: 14px; color: rgb(29, 29, 31); white-space: normal; flex-shrink: 1; }
.paseo-help { position: absolute; right: 4px; top: 4px; width: 36px; height: 36px; margin: 0; padding: 0; border-width: 0; border-radius: 18px; background-color: rgba(250, 250, 252, 0.94); color: rgb(0, 122, 255); font-size: 17px; -unity-text-align: middle-center; }
.paseo-help:hover { background-color: rgba(250, 250, 252, 0.94); }
.paseo-help:active { background-color: rgb(229, 229, 234); }
.paseo-bottom { position: absolute; left: 4px; right: 4px; bottom: 4px; }
.paseo-card { padding: 16px 18px; border-radius: 22px; background-color: rgba(250, 250, 252, 0.96); transition-property: translate, opacity; transition-duration: 0.28s, 0.28s; transition-timing-function: ease-out-cubic, ease-out-cubic; }
.paseo-card--entering { translate: 0 28px; opacity: 0; }
.paseo-card__row { flex-direction: row; align-items: center; justify-content: space-between; }
.paseo-card__title { font-size: 20px; color: rgb(29, 29, 31); white-space: normal; flex-shrink: 1; }
.paseo-card__subtitle { font-size: 15px; color: rgb(99, 99, 102); white-space: normal; margin-top: 4px; }
.paseo-chip { font-size: 12px; padding: 3px 10px; margin-left: 8px; border-radius: 10px; background-color: rgb(225, 245, 230); color: rgb(30, 110, 50); }
.paseo-team-corner { position: absolute; left: 0; top: 0; width: 56px; height: 56px; }

/* Name pills over the boxes: full screen, outside the safe area. */
.paseo-labels { position: absolute; left: 0; top: 0; right: 0; bottom: 0; }
.paseo-label { position: absolute; translate: -50% -100%; padding: 6px 12px; border-radius: 14px; background-color: rgba(250, 250, 252, 0.92); color: rgb(29, 29, 31); font-size: 15px; }

/* Team sheet */
.paseo-sheet { position: absolute; left: 0; right: 0; bottom: 0; max-height: 80%; padding: 8px 14px 14px 14px; border-top-left-radius: 22px; border-top-right-radius: 22px; background-color: rgb(242, 242, 247); transition-property: translate; transition-duration: 0.3s; transition-timing-function: ease-out-cubic; }
.paseo-sheet--closed { translate: 0 110%; }
.paseo-sheet__grabber { width: 36px; height: 5px; border-radius: 3px; align-self: center; margin-bottom: 8px; background-color: rgb(199, 199, 204); }
.paseo-sheet__header { flex-direction: row; justify-content: space-between; align-items: center; margin-bottom: 8px; }
.paseo-sheet__title { font-size: 20px; color: rgb(29, 29, 31); }
.paseo-sheet__scroll { flex-shrink: 1; }
.paseo-sheet__summary { font-size: 12px; color: rgb(110, 110, 115); white-space: normal; margin-bottom: 6px; }
.paseo-sheet__target { font-size: 15px; color: rgb(29, 29, 31); white-space: normal; margin-bottom: 8px; }
.paseo-sheet__hint { font-size: 14px; color: rgb(110, 110, 115); white-space: normal; }
.paseo-sheet__footer { margin-top: 4px; }
.paseo-section { padding: 10px 12px; margin-bottom: 10px; border-radius: 14px; background-color: rgb(255, 255, 255); }
.paseo-section__title { font-size: 13px; color: rgb(110, 110, 115); margin-bottom: 6px; }
.paseo-row { flex-direction: row; justify-content: space-between; padding: 6px 0; border-bottom-width: 1px; border-bottom-color: rgb(236, 236, 240); }
.paseo-row__title { font-size: 14px; color: rgb(29, 29, 31); flex-shrink: 0; }
.paseo-row__detail { font-size: 12px; color: rgb(110, 110, 115); white-space: normal; flex-shrink: 1; margin-left: 8px; -unity-text-align: middle-right; }
.tone-ok { color: rgb(30, 130, 60); }
.tone-neutral { color: rgb(110, 110, 115); }
.tone-warning { color: rgb(190, 120, 0); }
.tone-error { color: rgb(200, 40, 30); }
.paseo-nudges { flex-direction: row; flex-wrap: wrap; justify-content: space-between; }
.paseo-nudge { width: 49%; height: 40px; margin: 0 0 6px 0; border-width: 0; border-radius: 10px; background-color: rgb(239, 239, 244); color: rgb(0, 122, 255); font-size: 14px; }
.paseo-nudge:hover { background-color: rgb(239, 239, 244); }
.paseo-nudge:active { background-color: rgb(225, 225, 232); }
.paseo-nudge--wide { width: 100%; }
.paseo-link { margin: 0; padding: 4px 8px; border-width: 0; background-color: rgba(0, 0, 0, 0); color: rgb(0, 122, 255); font-size: 16px; }
.paseo-link:hover, .paseo-link:active { background-color: rgba(0, 0, 0, 0); }
.paseo-toast { font-size: 13px; color: rgb(110, 110, 115); -unity-text-align: middle-center; white-space: normal; margin-top: 6px; }
```

- [ ] **Step 3: Write `PaseoUi`**

`Assets/AncoRA/Scripts/Paseo/PaseoUi.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.AR
{
    /// <summary>
    /// The tour's interface on UI Toolkit: the welcome (permissions, then AR starts), the camera overlay (status pill,
    /// bottom card, name pills over the boxes) and the team sheet (five taps on the top-left corner). Views only draw;
    /// the decisions live in PaseoStartFlow, PaseoPermissionFlow, PaseoCard, PaseoCardChoice, PaseoMapRow and
    /// PaseoBoxNudge.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PaseoUi : MonoBehaviour
    {
        const string Tag = "[AncoRA Paseo]";
        const float RefreshSeconds = 0.2f;
        const float LabelGapMeters = 1.5f;

        [SerializeField] PaseoTour tour;
        [SerializeField] ARSession arSession;
        [SerializeField] StyleSheet styleSheet;
        [SerializeField] Font regularFont;
        [SerializeField] Font strongFont;

        readonly PaseoStartFlow flow = new();
        readonly PaseoTapGesture teamGesture = new();
        readonly List<PaseoLabelItem> labelItems = new();
        readonly List<Vector3> viewportPoints = new();
        readonly List<PaseoMapContent> shownContents = new();
        PaseoPermissionFlow permissions;
        VisualElement root;
        VisualElement safe;
        PaseoWelcomeView welcome;
        PaseoCameraView cameraView;
        PaseoLabelsView labels;
        PaseoTeamSheet sheet;
        Rect appliedSafeArea;
        float nextRefresh;

        public PaseoTour Tour => tour;
        public ARSession ArSession => arSession;
        public StyleSheet StyleSheet => styleSheet;
        public Font RegularFont => regularFont;
        public Font StrongFont => strongFont;

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(PaseoTour paseoTour, ARSession session, StyleSheet css, Font regular, Font strong)
        {
            tour = paseoTour;
            arSession = session;
            styleSheet = css;
            regularFont = regular;
            strongFont = strong;
        }

        void Awake()
        {
            // The SDK clocks wait for the welcome; the scene keeps the ARSession off until the permissions are answered.
            if (tour != null)
                tour.WaitForBegin();
#if UNITY_ANDROID && !UNITY_EDITOR
            permissions = new PaseoPermissionFlow(new AndroidPermissionSystem());
#else
            permissions = new PaseoPermissionFlow(new GrantedPermissionSystem());
#endif
        }

        void Start()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);
            root.AddToClassList("paseo-root");

            labels = new PaseoLabelsView();
            root.Add(labels.Root);
            safe = new VisualElement { name = "area-segura", pickingMode = PickingMode.Ignore };
            safe.AddToClassList("paseo-safe");
            root.Add(safe);

            cameraView = new PaseoCameraView();
            cameraView.HelpRequested += () => welcome.Show();
            safe.Add(cameraView.Root);

            var corner = new VisualElement { name = "esquina-equipo" };
            corner.AddToClassList("paseo-team-corner");
            corner.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (teamGesture.Tap(Time.realtimeSinceStartup))
                    sheet.Toggle();
            });
            safe.Add(corner);

            sheet = new PaseoTeamSheet();
            sheet.EditRequested += ApplyEdit;
            sheet.CopyRequested += CopyValues;
            sheet.RestoreRequested += () => tour.ForgetSavedAndReload();
            safe.Add(sheet.Root);

            welcome = new PaseoWelcomeView();
            welcome.Started += OnStartPressed;
            root.Add(welcome.Root);

            PaseoUiKit.ApplyFonts(root, regularFont, strongFont);
        }

        void OnStartPressed()
        {
            if (flow.State == PaseoStartState.Started)
            {
                welcome.Hide();
                return;
            }
            if (!flow.Press())
                return;
            welcome.ShowMessage("Responde los permisos de cámara y ubicación.");
            permissions.Begin(Time.realtimeSinceStartup);
        }

        void OnPermissionsAnswered()
        {
            flow.Answer(permissions.CameraGranted, permissions.LocationGranted);
            if (flow.State == PaseoStartState.NeedsCamera)
            {
                welcome.ShowMessage("Sin la cámara la app no puede funcionar. Vuelve a tocar Comenzar, o actívala en Ajustes > Apps > AncoRA Paseo > Permisos.");
                Debug.LogWarning($"{Tag} Permiso de cámara negado: la bienvenida se queda.");
                return;
            }
            welcome.Hide();
            arSession.enabled = true;
            tour.Begin();
            Debug.Log($"{Tag} Bienvenida cerrada: cámara permitida, ubicación {(flow.LocationGranted ? "permitida" : "negada")}; se enciende la sesión AR.");
        }

        void Update()
        {
            if (root == null)
                return;
            float now = Time.realtimeSinceStartup;
            if (flow.State == PaseoStartState.Requesting)
            {
                permissions.Update(now, Application.isFocused);
                if (permissions.Done)
                    OnPermissionsAnswered();
            }
            ApplySafeArea();
            if (now < nextRefresh)
                return;
            nextRefresh = now + RefreshSeconds;
            RefreshCamera();
            if (sheet.IsOpen)
                RefreshSheet();
        }

        void LateUpdate()
        {
            if (root != null)
                RefreshLabels();
        }

        // GuiSafeArea.Rect is in screen pixels with a top-left origin, which is what ScreenToPanel expects.
        void ApplySafeArea()
        {
            var area = GuiSafeArea.Rect;
            if (area == appliedSafeArea || root.panel == null)
                return;
            appliedSafeArea = area;
            var topLeft = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(area.xMin, area.yMin));
            var bottomRight = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(area.xMax, area.yMax));
            var screen = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(Screen.width, Screen.height));
            safe.style.left = topLeft.x;
            safe.style.top = topLeft.y;
            safe.style.right = screen.x - bottomRight.x;
            safe.style.bottom = screen.y - bottomRight.y;
        }

        void RefreshCamera()
        {
            bool tracking = ARSession.state == ARSessionState.SessionTracking;
            string status = PaseoStatusText.Describe(new PaseoStatusInputs
            {
                Error = tour.Error,
                ArUnsupported = ARSession.state == ARSessionState.Unsupported,
                SdkReady = tour.SdkReady,
                SecondsSinceStart = tour.SecondsSinceStart,
                MapStillLoading = tour.FirstMapStillLoading,
                Tracking = tracking,
                Buildings = tour.BuildingStatuses()
            }, out var level);
            cameraView.SetStatus(status, level);

            shownContents.Clear();
            viewportPoints.Clear();
            var cam = Camera.main;
            if (cam != null)
                foreach (var content in tour.Contents)
                    if (content != null && content.Box != null && content.Box.IsVisible)
                    {
                        shownContents.Add(content);
                        viewportPoints.Add(cam.WorldToViewportPoint(content.transform.position));
                    }
            int chosen = PaseoCardChoice.Pick(viewportPoints);
            cameraView.SetCard(chosen < 0
                ? PaseoCard.Choose(tour.SdkReady, tracking, null, null)
                : PaseoCard.Choose(tour.SdkReady, tracking, shownContents[chosen].BuildingName, shownContents[chosen].Description));
        }

        void RefreshLabels()
        {
            var cam = Camera.main;
            labelItems.Clear();
            foreach (var content in tour.Contents)
            {
                if (content == null || content.Box == null)
                    continue;
                Vector2? position = null;
                if (cam != null && root.panel != null && content.Box.IsVisible)
                {
                    var top = content.transform.position + content.transform.up * (content.SizeMeters.y * 0.5f + LabelGapMeters);
                    if (cam.WorldToViewportPoint(top).z > 0f)
                        position = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, top, cam);
                }
                labelItems.Add(new PaseoLabelItem(content.Placed ? content.BuildingName : content.BuildingName + " (sin colocar)", position));
            }
            if (labels.Set(labelItems))
                PaseoUiKit.ApplyFonts(labels.Root, regularFont, strongFont);
        }

        void RefreshSheet()
        {
            string summary = $"v{Application.version} · ARSession {ARSession.state} · SDK {(tour.SdkReady ? "listo" : "esperando")} · {tour.LocalizationSummary}";
            if (!string.IsNullOrEmpty(tour.Error))
                summary += $"\nERROR: {tour.Error}";
            if (sheet.SetStatus(summary, tour.MapRows()))
                PaseoUiKit.ApplyFonts(sheet.Root, regularFont, strongFont);
            var target = tour.EditableContent;
            sheet.SetTarget(target == null ? null : $"{target.BuildingName} · mapa {target.MapId}{(target.Placed ? "" : " · sin colocar")}");
        }

        void ApplyEdit(PaseoBoxEdit edit)
        {
            var content = tour.EditableContent;
            if (content == null)
                return;
            var position = PaseoBoxNudge.Move(content.LocalPosition, content.LocalYaw, edit.Right, edit.Closer, edit.Up);
            var size = Vector3.Max(content.SizeMeters + new Vector3(edit.Width, edit.Height, edit.Depth), Vector3.one * 0.5f);
            bool solid = edit.ToggleSolid && content.Box.HasSolidMaterial ? !content.Box.Solid : content.Box.Solid;
            tour.SaveEdit(content, position, content.LocalYaw + edit.Turn, size, solid);
        }

        void CopyValues()
        {
            string json = JsonUtility.ToJson(tour.BuildAdjustment());
            GUIUtility.systemCopyBuffer = json;
            Debug.Log($"{Tag} AJUSTE_PASEO {json}");
            try
            {
                string path = Path.Combine(Application.persistentDataPath, $"paseo-ajuste-campo-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.WriteAllText(path, json);
                Debug.Log($"{Tag} Ajuste guardado en {path}. Pegarlo en Assets/AncoRA/Paseo/ajuste-campo.json y correr ApplyFieldAdjustment.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} No se pudo guardar el ajuste en disco: {e.Message}");
            }
            sheet.ShowToast("Copiado al portapapeles y al log.");
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        sealed class AndroidPermissionSystem : IPaseoPermissionSystem
        {
            public bool Has(string permission) => UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission);
            public void Request(string permission) => UnityEngine.Android.Permission.RequestUserPermission(permission);
        }
#else
        sealed class GrantedPermissionSystem : IPaseoPermissionSystem
        {
            public bool Has(string permission) => true;
            public void Request(string permission) { }
        }
#endif
    }
}
```

- [ ] **Step 4: Wire `Prepare` and `Validate`; remove IMGUI**

En `PaseoSetup.cs`, agregar `using UnityEngine.UIElements;` y estas constantes junto a `AndroidApk`:

```csharp
        const string UiFolder = "Assets/AncoRA/Paseo/_Ui";
        const string UiStyleSheet = UiFolder + "/Paseo.uss";
        const string UiTheme = UiFolder + "/PaseoTema.tss";
        const string UiPanelSettings = UiFolder + "/PaseoPanel.asset";
        const string UiFontRegular = UiFolder + "/Fuentes/Inter-Regular.ttf";
        const string UiFontStrong = UiFolder + "/Fuentes/Inter-SemiBold.ttf";
```

En `PrepareCore`, reemplazar las tres líneas de `PaseoHud`/`PaseoFieldAdjust` (después de `tourComponent.Configure(...)`) por:

```csharp
            CreateInterface(root.transform, tourComponent);
```

y agregar:

```csharp
        static void CreateInterface(Transform parent, PaseoTour tour)
        {
            var session = Object.FindAnyObjectByType<ARSession>() ?? throw new InvalidOperationException("SimpleSample no contiene ARSession.");
            // AR starts after the welcome and the permissions (PaseoUi), so the camera dialog never meets a running session.
            session.enabled = false;
            var uiObject = new GameObject("Interfaz");
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<UIDocument>().panelSettings = LoadOrCreatePanelSettings();
            uiObject.AddComponent<PaseoUi>().Configure(tour, session, Load<StyleSheet>(UiStyleSheet), Load<Font>(UiFontRegular), Load<Font>(UiFontStrong));
        }

        static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Falta {path}.");

        static PanelSettings LoadOrCreatePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPanelSettings);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, UiPanelSettings);
            }
            settings.themeStyleSheet = Load<ThemeStyleSheet>(UiTheme);
            // iPhone points as panel units: font sizes and margins read like iOS ones on any phone.
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(390, 844);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0f;
            EditorUtility.SetDirty(settings);
            return settings;
        }
```

En `ValidateScene`, reemplazar el bloque de `huds`/`adjusters` por:

```csharp
            var uis = Object.FindObjectsByType<PaseoUi>(all, none);
            var arSessions = Object.FindObjectsByType<ARSession>(all, none);
            var document = uis.Length == 1 ? uis[0].GetComponent<UIDocument>() : null;
            if (uis.Length != 1 || tours.Length != 1 || uis[0].Tour != tours[0] || arSessions.Length != 1 ||
                uis[0].ArSession != arSessions[0] || uis[0].StyleSheet == null || uis[0].RegularFont == null ||
                uis[0].StrongFont == null || document == null || document.panelSettings == null ||
                document.panelSettings.themeStyleSheet == null)
                errors.Add("Debe haber una única interfaz (PaseoUi + UIDocument con PanelSettings y tema) conectada al PaseoTour, la ARSession, el estilo y las fuentes; correr Prepare.");
            if (arSessions.Length == 1 && arSessions[0].enabled)
                errors.Add("La ARSession debe quedar apagada en la escena: la enciende la bienvenida después de los permisos; correr Prepare.");
            if (Object.FindObjectsByType<TextMesh>(all, none).Length != 0)
                errors.Add("Quedan textos 3D (TextMesh) en la escena: los nombres van en la interfaz; correr Prepare.");
```

Borrar los scripts IMGUI:

```bash
git rm Assets/AncoRA/Scripts/Paseo/PaseoHud.cs Assets/AncoRA/Scripts/Paseo/PaseoHud.cs.meta Assets/AncoRA/Scripts/Paseo/PaseoFieldAdjust.cs Assets/AncoRA/Scripts/Paseo/PaseoFieldAdjust.cs.meta Assets/AncoRA/Scripts/Paseo/PaseoLabel.cs Assets/AncoRA/Scripts/Paseo/PaseoLabel.cs.meta
```

En `GuiSafeArea.cs`, el resumen pasa a «Usable screen rectangle in pixels with a top-left origin (IMGUI and
RuntimePanelUtils.ScreenToPanel use it): …».

- [ ] **Step 5: Verify**

Run en orden (cada uno con el Editor cerrado):
1. `...UnityBatch.ps1 -Mode tests` → Expected: todo pasa.
2. `...UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.Prepare` → Expected: «Escena creada … 7 mapa(s)».
3. `...UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.CheckNativeMapsInEditor` → Expected: validación OK y los 7 mapas cargan.
4. `...UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSmokeTest.Run` → Expected: «OK: Prepare, Validate y carga nativa…».
5. `Select-String -Path Assets/AncoRA -Recurse -Pattern "OnGUI|TextMesh" -Include *.cs` → Expected: solo `EdificioFacadeFrame`/tests si aplica, ninguna en el paseo.

- [ ] **Step 6: Commit**

```bash
git add Assets/AncoRA/Paseo/_Ui Assets/AncoRA/Scripts/Paseo/PaseoUi.cs* Assets/AncoRA/Scripts/GuiSafeArea.cs Assets/AncoRA/Editor/Paseo/PaseoSetup.cs Assets/Scenes/PaseoIngenieria.unity Assets/AncoRA/Paseo/_Materiales
git commit -m "Conectar la interfaz UI Toolkit y retirar el HUD IMGUI y los nombres 3D" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: APK 1.2.0, revisión en el Xiaomi y documentación

**Files:**
- Modify: `ProjectSettings/ProjectSettings.asset` (`bundleVersion: 1.2.0`, `AndroidBundleVersionCode: 7`)
- Modify: `CLAUDE.md`

- [ ] **Step 1: Bump the version and build**

Editar `bundleVersion: 1.1.4` → `1.2.0` y `AndroidBundleVersionCode: 6` → `7`. Run:
`...UnityBatch.ps1 -Mode method -Method AncorRA.Editor.PaseoSetup.BuildAndroid` → Expected: «APK listo».
Verificar con `aapt dump badging Builds/Android/AncoRAPaseo.apk`: `versionCode='7' versionName='1.2.0'`.

- [ ] **Step 2: Install and review on the phone**

`adb devices` → `adb -s <serial> install -r Builds/Android/AncoRAPaseo.apk`. Pedir al usuario que abra la app (no
abrirla por adb). En cada pantalla, `adb -s <serial> exec-out screencap -p > Logs/captura-<pantalla>.png` y revisar la
imagen: bienvenida (texto Inter legible, botón «Comenzar» dentro del área segura), cámara con píldora y tarjeta, nombre
flotante sobre una caja si hay un edificio a la vista, hoja del equipo (5 toques) con filas por mapa y ajuste.
Revisar `adb logcat -s Unity` por errores de UI Toolkit (tema faltante, fuentes) y por
`Bienvenida cerrada: cámara permitida`.

Si el texto no se ve con Inter (la fuente queda la del tema), cambiar `PaseoUiKit.ApplyFonts` para que también fije
`style.unityFont = regular/strong` además de `unityFontDefinition`, recompilar y volver a revisar.

- [ ] **Step 3: Document**

En `CLAUDE.md`:
- *Paseo virtual → Código*: quitar `PaseoHud`, `PaseoFieldAdjust` y `PaseoLabel`; agregar `PaseoUi` y las vistas en
  `Scripts/Paseo/Ui/` (`PaseoWelcomeView`, `PaseoCameraView`, `PaseoLabelsView`, `PaseoTeamSheet`, `PaseoUiKit`) y la lógica
  pura nueva (`PaseoBoxNudge`, `PaseoStartFlow`, `PaseoPermissionFlow`, `PaseoCard`, `PaseoCardChoice`, `PaseoTapGesture`,
  `PaseoMapRow`); actualizar el número de pruebas.
- *En terreno*: el panel del equipo es una hoja (5 toques arriba a la izquierda) con «Estado» y «Ajuste de caja»; los
  botones mueven la caja como la ve alguien parado frente a la X (Izquierda/Derecha, Más cerca/Más lejos, Bajar/Subir,
  Girar); «Copiar valores» y «Restaurar escena» siguen igual.
- *Trampas al tocar el código*: la regla de simetría IMGUI queda solo como historia (el paseo ya no usa IMGUI); nueva
  trampa: los estilos del tema de UI Toolkit fijan la fuente de cada texto, por eso `PaseoUiKit.ApplyFonts` la pone en
  línea y hay que llamarlo después de crear textos.
- Nota de versión **1.2.0**: interfaz UI Toolkit, bienvenida con permisos (la `ARSession` arranca apagada), `descripcion`
  en `edificio.json`.

- [ ] **Step 4: Commit**

```bash
git add ProjectSettings/ProjectSettings.asset CLAUDE.md
git commit -m "Subir a 1.2.0 con la interfaz nueva y documentarla" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
