# Continuous Live Normalizer
## About the application
This application is developed in C# using .NET 10. It uses [NAudio](https://github.com/naudio/NAudio), [ScottPlot](https://github.com/ScottPlot/ScottPlot) and a [VBVMR-API wrapper for C#](https://github.com/A-tG/Voicemeeter-Remote-API-dll-dynamic-wrapper).

The application is designed to calculate the volume of an audio source (WASAPI, WASAPI Loopback, WaveIn or ASIO) in dBfs (can be calibrated to match measured SPL) and automatically reduce the volume of a strip/bus in [Voicemeeter](https://voicemeeter.com/) when the input signal gets too loud over a set amount of time.

## Usage
After starting the application, select an audio API and device which will be used as the audio source. After adjusting the options, slick on "Start" to start the application.

The "Restart" button will "press" the "Stop" button, wait three seconds and press the "Start" button while also restarting the connection to the VBVMR-API.

## Options
<details>
<summary>API, Device, Channel</summary>

	Accepts: Selection	Default: -

	Selects the audio API and Device to use. 
	Channel refers to ASIO channels and is only available when selecting it as the API.
</details>

<details>
<summary>Use 32-bit FP</summary>

	Accepts: Boolean	Default: False

	Selects if 32-bit floating point audio should be used. 
	If disabled 24-bit PCM will be used.
</details>

<details>
<summary>Sample rate</summary>

	Accepts: Integer	Default: 48

	Sets the sample rate in kHz used for the Application (will also work when set SR is lower than the SR of the device).
</details>

<details>
<summary>Channel count</summary>

	Accepts: Integer	Default: 2

	Sets the number of channels the device has.

	Attention!
	The application will expect Mono(-ish) audio so this will be used to skip samples.
	For example, setting channel count to 2 will only use samples from the left channel and ignore the right channel to save processing power for mirrored signals.
	If all channels need to be used for the volume calculation, set this to 1 so that no sample gets skipped.
</details>

<details>
<summary>Averaging time</summary>

	Accepts: Integer	Default: 10

	Sets the time, in which the average will be calculated.
	This also dictates the interval at which the application will check if the average is higher than the threshold.
</details>

<details>
<summary>dB calibration</summary>

	Accepts: Float		Default: 0

	Sets the value that will be added to the calculated dB to match measured SPL.
</details>

<details>
<summary>Bus/Strip selection</summary>

	Accepts: Integer	Default: Bus 0

	Pressing the "Bus/Strip" button will switch between Bus (Voicemeeter output strips) and Strip (Voicemeeter input strips).
	The number dictates what Bus/Strip to use (0-7 for Voicemeeter Potato)
</details>

<details>
<summary>Threshold</summary>

	Accepts: Float		Default: -6

	Sets the threshold at which the application will reduce the volume, if the average exceeds it.
</details>

<details>
<summary>Return to</summary>

	Accepts: Float*		Default: -12

	Sets the level to which the application will reduce the volume to.

	Note:
	Its advised to keep this a bit under the threshold to prevent micro adjustments becuase of minor fluctuations.

	*Although the value is read as a float and used to calculate the distance to the last average, this distance will be cast to an Integer.
</details>

<details>
<summary>Fade rate</summary>

	Accepts: Integer	Default: 10

	Dictates the speed at rate the volume will decrease.
</details>