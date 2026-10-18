import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ComponentProps } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OtpGeneratorForm } from './OtpGeneratorForm';

function renderForm(props: Partial<ComponentProps<typeof OtpGeneratorForm>> = {}) {
  const onGenerate = vi.fn();
  render(
    <OtpGeneratorForm onGenerate={onGenerate} isGenerating={false} error={null} {...props} />,
  );
  return { onGenerate };
}

beforeEach(() => {
  // Only Date is faked, so user-event and React keep their normal timers.
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 9, 17, 3, 56, 44));
});

afterEach(() => {
  vi.useRealTimers();
});

describe('OtpGeneratorForm', () => {
  it('renders the labelled fields and buttons', () => {
    renderForm();

    expect(screen.getByLabelText('User Id')).toBeInTheDocument();
    expect(screen.getByLabelText('Date and time')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Now' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Generate password' })).toBeInTheDocument();
  });

  it('defaults the date and time to the current moment', () => {
    renderForm();

    expect(screen.getByLabelText('Date and time')).toHaveValue('2026-10-17T03:56:44.000');
  });

  it('limits the user id to 30 characters', () => {
    renderForm();

    expect(screen.getByLabelText('User Id')).toHaveAttribute('maxlength', '30');
  });

  it('submits the trimmed user id and the date as an ISO string', async () => {
    const user = userEvent.setup();
    const { onGenerate } = renderForm();

    await user.type(screen.getByLabelText('User Id'), '  john.doe  ');
    fireEvent.change(screen.getByLabelText('Date and time'), {
      target: { value: '2026-10-16T12:30:00' },
    });
    await user.click(screen.getByRole('button', { name: 'Generate password' }));

    expect(onGenerate).toHaveBeenCalledTimes(1);
    expect(onGenerate).toHaveBeenCalledWith({
      userId: 'john.doe',
      dateTime: new Date(2026, 9, 16, 12, 30, 0).toISOString(),
    });
  });

  it('submits the default date and time when the user does not change them', async () => {
    const user = userEvent.setup();
    const { onGenerate } = renderForm();

    await user.type(screen.getByLabelText('User Id'), 'john.doe');
    await user.click(screen.getByRole('button', { name: 'Generate password' }));

    expect(onGenerate).toHaveBeenCalledWith({
      userId: 'john.doe',
      dateTime: new Date(2026, 9, 17, 3, 56, 44).toISOString(),
    });
  });

  it('submits when the user presses Enter in the user id field', async () => {
    const user = userEvent.setup();
    const { onGenerate } = renderForm();

    await user.type(screen.getByLabelText('User Id'), 'john.doe{Enter}');

    expect(onGenerate).toHaveBeenCalledTimes(1);
  });

  it('puts the current moment back when Now is pressed', async () => {
    const user = userEvent.setup();
    renderForm();
    fireEvent.change(screen.getByLabelText('Date and time'), {
      target: { value: '2020-01-01T00:00:00' },
    });

    await user.click(screen.getByRole('button', { name: 'Now' }));

    expect(screen.getByLabelText('Date and time')).toHaveValue('2026-10-17T03:56:44.000');
  });

  it.each(['', '   '])('requires a user id (input "%s")', async (value) => {
    const user = userEvent.setup();
    const { onGenerate } = renderForm();
    if (value !== '') {
      await user.type(screen.getByLabelText('User Id'), value);
    }

    await user.click(screen.getByRole('button', { name: 'Generate password' }));

    expect(screen.getByRole('alert')).toHaveTextContent('User Id is required.');
    expect(onGenerate).not.toHaveBeenCalled();
  });

  it('requires a valid date and time', async () => {
    const user = userEvent.setup();
    const { onGenerate } = renderForm();
    await user.type(screen.getByLabelText('User Id'), 'john.doe');
    fireEvent.change(screen.getByLabelText('Date and time'), { target: { value: '' } });

    await user.click(screen.getByRole('button', { name: 'Generate password' }));

    expect(screen.getByRole('alert')).toHaveTextContent('A valid date and time is required.');
    expect(onGenerate).not.toHaveBeenCalled();
  });

  it('removes the validation message as soon as the user edits the form', async () => {
    const user = userEvent.setup();
    renderForm();
    await user.click(screen.getByRole('button', { name: 'Generate password' }));
    expect(screen.getByRole('alert')).toBeInTheDocument();

    await user.type(screen.getByLabelText('User Id'), 'j');

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows an error returned by the API', () => {
    renderForm({ error: 'Too many incorrect attempts. Please try again in 42 second(s).' });

    expect(screen.getByRole('alert')).toHaveTextContent(
      'Too many incorrect attempts. Please try again in 42 second(s).',
    );
  });

  it('shows its own validation message instead of an older API error', async () => {
    const user = userEvent.setup();
    renderForm({ error: 'An old API error.' });

    await user.click(screen.getByRole('button', { name: 'Generate password' }));

    expect(screen.getByRole('alert')).toHaveTextContent('User Id is required.');
  });

  it('disables the submit button and says so while generating', () => {
    renderForm({ isGenerating: true });

    expect(screen.getByRole('button', { name: 'Generating…' })).toBeDisabled();
  });
});
