import './style.css';
import { startHybridPreview } from './hlp/index.js';

// Unity camera stream + input forwarding. The small "HLP" box is the preview's status panel; drag it
// anywhere or collapse it. Pass { panel: false } to hide it entirely.
startHybridPreview(document.getElementById('stream'));

// Your H5 UI goes in #app (index.html) and here. Talk to Unity through the global HybridBridge:
//   HybridBridge.send('door', { id: 'A1', open: true });       // C#: HybridBridge.On<Door>("door", ...)
//   HybridBridge.on('sensor', (data) => console.log(data));    // C#: HybridBridge.Send("sensor", reading)
