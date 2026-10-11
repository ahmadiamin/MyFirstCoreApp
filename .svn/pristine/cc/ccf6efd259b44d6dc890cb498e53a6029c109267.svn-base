// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener("DOMContentLoaded", function () {

    const toggle = document.getElementById("sidebarToggle");

    if (!toggle)
        return;

    toggle.addEventListener("click", function () {

        document.body.classList.toggle(
            "sidebar-collapsed");

    });

});

/* ============================================
   InputMask Global Handler
   ============================================ */
$(document).ready(function () {
    // فعال‌سازی data-mask ها
    $('[data-mask]').inputmask();

    // 🆕 مبلغ با کاما (روش دستی)
    function formatMoney(value) {
        if (value === null || value === undefined) return '';
        // تبدیل به رشته
        value = value.toString();
        // حذف هر چیزی غیر از عدد و منفی
        var num = value.replace(/[^0-9-]/g, '');
        if (num === '' || num === '-') return num;
        // جدا کردن سه‌رقمی
        return num.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    // 🆕 فرمت کردن مقدار اولیه (موقع بارگذاری صفحه)
    $('.input-mask-money').each(function () {
        var val = $(this).val();
        if (val) {
            $(this).val(formatMoney(val));
        }
    });

    // فرمت کردن موقع تایپ
    $(document).on('input', '.input-mask-money', function () {
        var cursorPos = this.selectionStart;
        var oldValue = this.value;
        var newValue = formatMoney(oldValue);

        if (oldValue !== newValue) {
            this.value = newValue;
            // تنظیم مکان کرسر
            var diff = newValue.length - oldValue.length;
            this.setCursorPosition
                ? this.setSelectionRange(cursorPos + diff, cursorPos + diff)
                : null;
        }
    });

    // 🆕 موقع submit، کاماها رو حذف کن
    $(document).on('submit', 'form', function () {
        $(this).find('.input-mask-money').each(function () {
            var val = $(this).val();
            if (val) {
                $(this).val(val.replace(/,/g, ''));
            }
        });
    });

    // موبایل (0912-345-6789)
    $('.input-mask-mobile').inputmask({
        mask: '0999-999-9999',
        placeholder: '',
        clearIncomplete: false,
        showMaskOnHover: false
    });

    // تلفن ثابت (021-12345678)
    $('.input-mask-phone').inputmask({
        mask: '099-99999999',
        placeholder: '',
        clearIncomplete: false,
        showMaskOnHover: false
    });

    // کد پستی (12345-67890)
    $('.input-mask-postal-code').inputmask({
        mask: '99999-99999',
        placeholder: '',
        clearIncomplete: false,
        showMaskOnHover: false
    });

    // کد ملی (10 رقم)
    $('.input-mask-national-id').inputmask({
        mask: '9999999999',
        placeholder: '',
        clearIncomplete: false,
        showMaskOnHover: false
    });

    // کد اقتصادی (12 رقم)
    $('.input-mask-economic-code').inputmask({
        mask: '999999999999',
        placeholder: '',
        clearIncomplete: false,
        showMaskOnHover: false
    });
});

/* ============================================
   اعداد فارسی
   ============================================ */

// تبدیل اعداد لاتین به فارسی
function toPersianNumber(input) {
    if (input === null || input === undefined) return '';
    const persianDigits = '۰۱۲۳۴۵۶۷۸۹';
    return input.toString().replace(/\d/g, d => persianDigits[d]);
}

// تبدیل اعداد فارسی به لاتین
function toEnglishNumber(input) {
    if (input === null || input === undefined) return '';
    return input.toString()
        .replace(/[۰-۹]/g, d => '۰۱۲۳۴۵۶۷۸۹'.indexOf(d))
        .replace(/[٠-٩]/g, d => '٠١٢٣٤٥٦٧٨٩'.indexOf(d));
}

// 🆕 فرمت عدد بدون کاما (برای SortOrder, TaxRate)
function formatNumberSimple(value) {
    if (value === null || value === undefined || value === '') return '';

    let english = toEnglishNumber(value.toString());
    english = english.replace(/[^0-9.-]/g, '');

    if (english === '' || english === '-') return english;

    // 🆕 فقط بخش صحیح
    const parts = english.split('.');
    const integerPart = parts[0];

    return toPersianNumber(integerPart);
}

// 🆕 فرمت عدد با کاما (برای مبالغ)
function formatNumberWithComma(value) {
    if (value === null || value === undefined || value === '') return '';

    let english = toEnglishNumber(value.toString());
    english = english.replace(/[^0-9.-]/g, '');

    if (english === '' || english === '-') return english;

    // 🆕 حذف اعشار
    const num = Math.floor(parseFloat(english));
    if (isNaN(num)) return '';

    const formatted = num.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return toPersianNumber(formatted);
}

// تبدیل مقدار نمایشی به مقدار ارسالی
function normalizeNumber(value) {
    if (value === null || value === undefined) return '';
    let english = toEnglishNumber(value.toString());
    english = english.replace(/[^0-9.-]/g, '');

    // 🆕 فقط بخش صحیح
    const parts = english.split('.');
    return parts[0] || '';
}

$(document).ready(function () {

    // ---------- تابع sync ----------
    function syncDisplayToHidden($displayInput) {
        const targetId = $displayInput.data('target');
        if (!targetId) return;

        const displayVal = $displayInput.val();
        const hiddenVal = normalizeNumber(displayVal);

        $('#' + targetId).val(hiddenVal);
    }

    // ---------- فرمت اولیه ----------
    $('input.persian-number').each(function () {
        const $input = $(this);
        const targetId = $input.data('target');
        const val = $input.val();

        let initialVal = val;
        if (targetId) {
            const hiddenVal = $('#' + targetId).val();
            if (hiddenVal) {
                initialVal = hiddenVal;
            }
        }

        if (initialVal) {
            // 🆕 اگه کلاس persian-number-comma داره، با کاما
            if ($input.hasClass('persian-number-comma')) {
                $input.val(formatNumberWithComma(initialVal));
            } else {
                $input.val(formatNumberSimple(initialVal));
            }
            syncDisplayToHidden($input);
        }
    });

    // ---------- sync موقع تایپ ----------
    $(document).on('input', 'input.persian-number', function () {
        const $input = $(this);
        const cursorPos = this.selectionStart;
        const oldVal = $input.val();

        // 🆕 انتخاب تابع فرمت بر اساس کلاس
        let newVal;
        if ($input.hasClass('persian-number-comma')) {
            newVal = formatNumberWithComma(oldVal);
        } else {
            newVal = formatNumberSimple(oldVal);
        }

        if (oldVal !== newVal) {
            $input.val(newVal);
            const diff = newVal.length - oldVal.length;
            this.setSelectionRange(cursorPos + diff, cursorPos + diff);
        }

        syncDisplayToHidden($input);
    });

});

// راه‌اندازی Persian Date Picker
$(document).ready(function () {
    if ($.fn.persianDatepicker) {
        $('.persian-date-picker').persianDatepicker({
            format: 'YYYY/MM/DD',
            initialValue: true,
            autoClose: true,
            observer: true,
            calendar: {
                persian: {
                    locale: 'fa',
                    showHint: true,
                    leapYearMode: 'algorithmic'
                }
            }
        });
    }
});

