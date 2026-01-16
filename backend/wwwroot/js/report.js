const setupSlider = (element) => {
    const range = element.querySelector('[data-image-comparison-range]');
    const slider = element.querySelector('[data-image-comparison-slider]');
    const overlay = element.querySelector('[data-image-comparison-overlay]');

    const move = (e) => {
        const value = e.target.value;
        slider.style.left = `${value}%`;
        overlay.style.width = `${value}%`;
    };

    range.addEventListener('input', move);
    range.addEventListener('change', move);
};

document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-component="image-comparison-slider"]').forEach(setupSlider);
});
