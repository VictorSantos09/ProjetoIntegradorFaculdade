function playAudio(elementId) {
    const audio = document.getElementById(elementId);
    if (audio) {
        audio.play();
    }
}

function pauseAudio(elementId) {
    const audio = document.getElementById(elementId);
    if (audio) {
        audio.pause();
    }
}